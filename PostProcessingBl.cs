using AutoMapper;
using OlameterFramework.EventBus;
using OlameterFramework.EventBus.IntegrationEventLog;
using OlameterFramework.OFramework.ConfigUtils;
using OlameterFramework.OFramework.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using WFMS.WorkOrderExecution.BL.IntegrationEvents.Events;
using WFMS.WorkOrderExecution.BL.PostProcess;
using WFMS.WorkOrderExecution.DAL;
using WFMS.WorkOrderExecution.Model;
using WFMS.WorkOrderExecution.Model.Messages;
using Amazon.S3;
using Amazon.S3.Model;
using Confluent.Kafka;
using Xamarin.Forms;

namespace WFMS.WorkOrderExecution.BL.BusinessLayer
{
    public interface IPostProcessingBl
    {
        List<WorkOrderModel> RunPostProcess(List<WorkOrderModel> workOrders, string username,string token =null);

        WorkOrderModel RetryPostProcess(WorkOrderModel wo, string username);

        WorkOrderModel SaveAfterApproveAudit(WorkOrderModel wo, string username);

        void Init(JsonDocument args);
    }

    public class PostProcessingBl : OlameterFramework.OFramework.BL.BaseCoreEntityBl<PostProcessingModel, PostProcessingDal>, IPostProcessingBl
    {
        private readonly ApplicationDbContext _context;
        private readonly IHistoryBl _historyBl;
        private readonly IAuditLogBl _auditBl;
        public readonly IWoGenericEnumValuesBl _gevBl;
        private readonly IMapper _mapper;
        private readonly IMessageIntegrationEventLogService _eventLogService;
        private readonly IWorkOrderConfig _workOrderConfig;
        private readonly IServiceProvider _serviceProvider;
        private readonly AWSCredentials _awsCredentials;

        public PostProcessingBl(ApplicationDbContext context, IHistoryBl historyBl, IAuditLogBl auditBl, IWoGenericEnumValuesBl gevBl,
            IMapper mapper, IMessageIntegrationEventLogService eventLogService, IWorkOrderConfig workOrderConfig,
            IServiceProvider serviceProvider,AWSCredentials awsCredential = null) : base(context)
        {
            _context = context;
            _historyBl = historyBl;
            _auditBl = auditBl;
            _gevBl = gevBl;
            _mapper = mapper;
            _eventLogService = eventLogService;
            _workOrderConfig = workOrderConfig;
            _serviceProvider = serviceProvider;
            _awsCredentials = awsCredential;
        }

        public List<WorkOrderModel> RunPostProcess(List<WorkOrderModel> workOrders, string username,string token=null)
        {
            if (workOrders == null)
            {
                throw new ArgumentNullException(nameof(workOrders));
            }

            if (workOrders.Count == 0)
            {
                return new List<WorkOrderModel>();
            }

            var updatedWorkOrders = new List<WorkOrderModel>(workOrders.Count);

            foreach (var workOrder in workOrders)
            {
                updatedWorkOrders.Add(RunPostProcessInternal(workOrder, username, token));
            }

            return updatedWorkOrders;
        }

        private WorkOrderModel RunPostProcessInternal(WorkOrderModel wo, string username,string token=null)
        {
            if (wo == null)
            {
                throw new ArgumentNullException(nameof(wo));
            }

            WorkOrderModel updatedWo = wo;

            if (updatedWo.StatusWebString == StatusWeb.Completed.ToString() || updatedWo.StatusWebString == StatusWeb.ReturnToUtility.ToString())
            {
                if(updatedWo.StatusWebString == StatusWeb.Completed.ToString())
                    updatedWo = ExecuteConfiguredPostProcess(updatedWo, username, token);

                if (updatedWo.PostProcessStatusString == PostProcessStatus.PostProcessingWoStillOpen.ToStringValue() && (!updatedWo.AuditMode || updatedWo.Audited))
                {
                    updatedWo = ExecutePostProcess(updatedWo, username);                    
                }                
            }

            return updatedWo;
        }

        public WorkOrderModel RetryPostProcess(WorkOrderModel wo, string username)
        {
            if (wo.StatusWebString == StatusWeb.Completed.ToString() && wo.PostProcessStatusString == PostProcessStatus.PostProcessingEtvFail.ToStringValue() && (!wo.AuditMode || wo.Audited))
            {
                ExecutePostProcess(wo, username);
            }

            return wo;
        }


        public WorkOrderModel SaveAfterApproveAudit(WorkOrderModel wo, string username)
        {
            var status = _gevBl.GetOrCreate(PostProcessStatus.PostProcessingWoEtvStart.GetEnumName(), PostProcessStatus.PostProcessingWoEtvStart.ToStringValue(), andSave: true);
            wo.PostProcessStatusId = status.Id;
            wo.PostProcessStatusString = PostProcessStatus.PostProcessingWoEtvStart.ToStringValue();

            var model = new HistoryModel
            {
                WorkOrder = wo,
                TimeStamp = DateTime.UtcNow,
                UserName = username,
                Audits = _auditBl.CreateAuditList(wo),
                ActivityDetailString = ActivityDetailConstants.PostProcessStatusChanged,
                ActivityTypeString = ActivityTypeConstants.StatusChanged,
                PostProcessingString = PostProcessStatus.PostProcessingWoEtvStart.ToStringValue()
            };

            _historyBl.Create(model, false);

            return wo;
        }

        public void Init(JsonDocument args)
        {
            //Nothing to init
        }

        private WorkOrderModel ExecutePostProcess(WorkOrderModel wo, string username)
        {
            var updatedWo = SaveAfterApproveAudit(wo, username);
            WorkOrderReadyForExportPostProcess(updatedWo);

            return updatedWo;
        }

        private void WorkOrderReadyForExportPostProcess(WorkOrderModel wo)
        {
            if(_awsCredentials== null)
                LoggerUtil.LogWarning("AwsCredentials is not set");
            
            using (AmazonS3Client client = new AmazonS3Client(_awsCredentials,
                       RegionEndpoint.GetBySystemName(ConfigUtil.GetValue("Amazon:Region"))))
            {
                string bucket = ConfigUtil.GetValue("rootbucketname");
                var woEvent = _mapper.Map<WorkOrderReadyForExportIntegrationEventModel>(wo);
                woEvent.Model.Files = _context.Set<FileLocationModel>().Where(x => x.WorkOrder.Name == woEvent.WorkOrderId).ToList()
                    .Select(x =>
                    {
                        string url = client.GetPreSignedURL(new GetPreSignedUrlRequest
                        {
                            BucketName = bucket,
                            Key = x.Location,
                            ContentType = x.FileType,
                            Expires = DateTime.Now.AddSeconds(604800)
                        });

                        return url;
                    }).ToList();
                _eventLogService.Publish<WorkOrderReadyForExportIntegrationEvent>(EventAction.Update, woEvent);
            }
            
        }

        private WorkOrderModel ExecuteConfiguredPostProcess(WorkOrderModel wo, string username,string token=null)
        {
            var config = _workOrderConfig.GetWorkOrderConfig(wo);
            LoggerUtil.LogDebug($"Run ExecuteConfigurePostProcess WorkOrder {wo.Name}:{wo.JsonData.ToJsonString()} username:{username} config:{config.ToJsonString()}");
            foreach (var postProcess in config.PostProcess)
            {
                try
                {
                    var postProcessBl = PostProcessFactory.CreatePostProcess(_serviceProvider, postProcess.PostProcess, postProcess.Properties);
                    postProcessBl.RunPostProcess(wo, username,token);
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError(ex, string.Format("An error occurred while executing postprocess {0}", postProcess.PostProcess));
                }                
            }

            return wo;
        }

    }
}
