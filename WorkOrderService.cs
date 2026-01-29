using AutoMapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Olameter.WFMS.WorkOrderExecution.API.V1;
using Olameter.WFMS.WorkOrderExecution.Common.V1;
using OlameterFramework.Auth.Policies;
using OlameterFramework.OFramework.ConfigUtils;
using OlameterFramework.OFramework.gRPC;
using OlameterFramework.OFramework.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using WFMS.WorkOrderExecution.BL.BusinessLayer;
using WFMS.WorkOrderExecution.DAL.Move;
using WFMS.WorkOrderExecution.Model;
using WFMS.WorkOrderExecution.Model.Dto; 

namespace WFMS.WorkOrderExecution.API.Services
{
    public class WorkOrderService : WorkOrderApiService.WorkOrderApiServiceBase
    {
        private readonly IWorkOrderBl _workOrderApiBl;
        private readonly IWorkOrderVersionBl _workOrderVersionBl;
        private readonly FileManagerBl _fileManagerBl;
        private readonly IHistoryBl _historyBl;
        private readonly IAggregateBl _auditBl;
        private readonly IMapper _mapper;
        private readonly string _claimName;
        private readonly IWorkOrderSchedulerBl _workOrderSchedulerBl;
        private readonly IWorkOrderBlackOutBl _workOrderBlackOutBl;
        private readonly IContractBl _contractBl;
        private readonly IReportHistoryBl _reportHistoryBl;
        private readonly IAutoDialerBl _autoDialerBl;
        private readonly Dictionary<string, string> _awsInfo;

        public WorkOrderService(IServiceProvider serviceProvider)
        {
            _workOrderApiBl = serviceProvider.GetService<IWorkOrderBl>();
            _workOrderVersionBl = serviceProvider.GetService<IWorkOrderVersionBl>();
            _fileManagerBl = serviceProvider.GetService<FileManagerBl>();
            _historyBl = serviceProvider.GetService<IHistoryBl>();
            _auditBl = serviceProvider.GetService<IAggregateBl>();
            _mapper = serviceProvider.GetService<IMapper>();
            _workOrderSchedulerBl = serviceProvider.GetService<IWorkOrderSchedulerBl>();
            _workOrderBlackOutBl = serviceProvider.GetService<IWorkOrderBlackOutBl>();
            _contractBl = serviceProvider.GetService<IContractBl>();
            _reportHistoryBl = serviceProvider.GetService<IReportHistoryBl>();
            _autoDialerBl = serviceProvider.GetService<IAutoDialerBl>();

            _claimName = ConfigUtil.GetValue("UserInfo:Identifier");
            _awsInfo = new Dictionary<string, string>{
                {"Key", ConfigUtil.GetValue("Aws:Key")},
                {"Secret", ConfigUtil.GetValue("Aws:Secret")},
                {"Region", ConfigUtil.GetValue("Aws:Region")},
                {"UserPool", ConfigUtil.GetValue("Aws:UserPool:Wfms")}
            };
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public async override Task GetListByContractStream(ListByContractRequest request, IServerStreamWriter<WorkOrderResponse> responseStream, ServerCallContext context)
        {
            OlameterFramework.OUIFramework.RequestDto filter = _mapper.Map<OlameterFramework.OUIFramework.RequestDto>(request.Filter);//TODO: here i should recieve the same objet than in the fwor... also, improve name
            var installDateOption = System.Enum.Parse<InstallDateOption>(request.InstallDateOption);
            var startD = request.StartDate.ToDateTime();
            var endD = request.EndDate.ToDateTime();
            var contract = _contractBl.GetByName(request.ContractName);

            IAsyncEnumerator<WorkOrderModel> result = _workOrderApiBl.GetAllAsync(request.ContractName, startD, endD, request.WoStatusNames.ToList(), filter, installDateOption).GetAsyncEnumerator();

            while (await result.MoveNextAsync())
            {
                var wo = _mapper.Map<WorkOrder>(result.Current);
                wo.ContractUseCertification = contract.UseCertification;
                wo.JsonData = _workOrderApiBl.GetRequestedFieldsOnly(request.RequestedFields.ToList(), wo.JsonData);
                await responseStream.WriteAsync(new WorkOrderResponse { WorkOrder = wo });
            }
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<WorkOrdersResponseList> GetListByContract(ListByContractRequest request, ServerCallContext context)
        {
            OlameterFramework.OUIFramework.RequestDto filter = _mapper.Map<OlameterFramework.OUIFramework.RequestDto>(request.Filter);//TODO: here i should recieve the same objet than in the fwor... also, improve name
            var installDateOption = InstallDateOption.Custom;
            System.Enum.TryParse<InstallDateOption>(request.InstallDateOption, out installDateOption);
            var startD = request.StartDate.ToDateTime();
            var endD = request.EndDate.ToDateTime();
            var contract = _contractBl.GetByName(request.ContractName);
            var woNcount = _workOrderApiBl.GetAll(request.ContractName, startD, endD, request.WoStatusNames.ToList(), filter, installDateOption);
            var wos = woNcount.Select(x => _mapper.Map<WorkOrder>(x)).ToList();

            foreach (var wo in wos)
            {
                wo.ContractUseCertification = contract.UseCertification;
                wo.JsonData = _workOrderApiBl.GetRequestedFieldsOnly(request.RequestedFields.ToList(), wo.JsonData);
            }

            WorkOrdersResponseList result = new();
            result.WorkOrders.AddRange(wos);

            return Task.FromResult(result);
        }

        //[Authorize(Policy = AuthPolicies.ForVisitor)] Enable it when ready
        [AllowAnonymous]
        public override Task<ValidateAccountAndGetWorkOrderListResponse> ValidateAccountAndGetWorkOrderList(ValidateAccountAndGetWorkOrderListRequest request, ServerCallContext context)
        {
            OlameterFramework.OUIFramework.RequestDto filter = _mapper.Map<OlameterFramework.OUIFramework.RequestDto>(request.Filter);
            var installDateOption = InstallDateOption.All;
            var date = DateTime.Now;
            var woList = _workOrderApiBl.GetAll(request.ContractName, date, date, request.WoStatusNames.ToList(), filter, installDateOption);
            var wos = woList.Select(x => _mapper.Map<WorkOrder>(x)).ToList();

            if (wos.Count == 0)
            {
                throw new RpcException(new Grpc.Core.Status(StatusCode.NotFound, $"Account not found"));
            }

            ValidateAccountAndGetWorkOrderListResponse result = new();
            result.WorkOrders.AddRange(wos);

            //TODO get/return token

            return Task.FromResult(result);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<TotalCountResponse> GetTotalCount(ListByContractRequest request, ServerCallContext context)
        {
            OlameterFramework.OUIFramework.RequestDto filter = _mapper.Map<OlameterFramework.OUIFramework.RequestDto>(request.Filter);//TODO: here i should recieve the same objet than in the fwor... also, improve name
            var installDateOption = InstallDateOption.Custom;
            System.Enum.TryParse<InstallDateOption>(request.InstallDateOption, out installDateOption);
            var startD = request.StartDate.ToDateTime();
            var endD = request.EndDate.ToDateTime();
            long totalCount = _workOrderApiBl.GetTotalCount(request.ContractName, startD, endD, request.WoStatusNames.ToList(), filter, installDateOption);

            TotalCountResponse result = new();
            result.TotalCount = totalCount;

            return Task.FromResult(result);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<ColumnValuesResponse> GetPreFilterColumnValues(ColumnValuesRequest request, ServerCallContext context)
        {
            var installDateOption = InstallDateOption.Custom;
            System.Enum.TryParse<InstallDateOption>(request.InstallDateOption, out installDateOption);
            var startD = request.StartDate.ToDateTime();
            var endD = request.EndDate.ToDateTime();
            List<string> values = _workOrderApiBl.GetPreFilterColumnValues(request.ContractName, startD, endD, request.ColumnName, request.ColumnFilterValue, installDateOption);

            ColumnValuesResponse result = new();
            result.Values.AddRange(values);

            return Task.FromResult(result);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<WorkOrderResponse> Get(GetRequest request, ServerCallContext context)
        {
            var workorder = _workOrderApiBl.GetFirst(x => x.Name == request.WorkOrderName);

            if (workorder != null)
            {
                var property = ConfigUtil.GetValue("UserInfo:Identifier");
                var username = ServerCallContextUtils.GetClaimValue(context, property, _awsInfo);

                if (workorder.Versions != null)
                {
                    workorder.Versions = workorder.Versions.OrderByDescending(x => x.CreatedOn).ToList();
                }

                _workOrderApiBl.CreateFirstTask(workorder.Name, username);
            }

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(workorder)
            });
        }

        [AllowAnonymous]
        public override Task<HealthzResponse> Healthz(Empty request, ServerCallContext context)
        {
            var (version, message) = HealthzUtils.HealthzCheck();

            return Task.FromResult(new HealthzResponse
            {
                Message = message,
                Version = version
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderResponse> Update(WorkOrderRequest request, ServerCallContext context)
        {
            WorkOrderModel workOrder = _mapper.Map<WorkOrderModel>(request.WorkOrder);

            string username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);
            workOrder.Tasks = null;

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(_workOrderApiBl.Update(workOrder, username, true))
            });
        }

        //Used in Integration Test
        [Authorize(Policy = CustomAuthPolicies.ForIntegrationTest)]
        public override Task<WorkOrderResponse> Create(WorkOrderRequest request, ServerCallContext context)
        {
            WorkOrderModel workOrder = _mapper.Map<WorkOrderModel>(request.WorkOrder);

            string username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(_workOrderApiBl.Create(workOrder, username, true))
            });
        }

        //Used in Integration Test
        [Authorize(Policy = CustomAuthPolicies.ForIntegrationTest)]
        public override Task<WorkOrderResponse> Delete(WorkOrderRequest request, ServerCallContext context)
        {
            WorkOrderModel workOrder = _mapper.Map<WorkOrderModel>(request.WorkOrder);

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(_workOrderApiBl.Delete(workOrder, "Integration Test", true))
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<DeleteFileResponse> DeleteFile(DeleteFileRequest request,
            ServerCallContext context)
        {
            var response = new DeleteFileResponse();
            var fileDetails = _fileManagerBl.FindByName(request.Name);
            try
            {
                response.Result = _fileManagerBl.DeleteFile(request.Name);
            }
            catch (Exception e)
            {
                LoggerUtil.LogError(e, string.Format("An exception occurred while deleting {0}", request.Name));
                throw;
            }

            if (response.Result)
            {

                var username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);

                var model = new HistoryModel
                {
                    WorkOrder = _workOrderApiBl.GetOne(x => x.JsonData.RootElement.GetProperty("WorkOrder.WorkOrderId").GetString() == fileDetails.WorkOrderName),
                    TimeStamp = DateTime.UtcNow,
                    UserName = username,
                    ActivityDetailString = ActivityDetailConstants.WorkOrderUpdated,
                    ActivityTypeString = ActivityType.ActivityTypeStatusName.ToStringValue(),
                    PostProcessingString = PostProcessStatus.PostProcessingNone.ToStringValue()
                };
                _historyBl.Create(model);
            }
            return Task.FromResult(response);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override async Task<FilesResponse> GetFiles(FileRequest request, ServerCallContext context)
        {
            var workOrderFiles = await _fileManagerBl.GetWorkOrderFiles(request.WorkOrderName);
            var workOrderFilesResponse = new FilesResponse();
            foreach (var file in workOrderFiles)
            {
                workOrderFilesResponse.Files.Add(new WorkOrderFile() { FileName = file.fileName, FileUrl = file.fileUrl, Name = file.name, TimeStamp = file.createdOn.ToTimestamp() });
            }
            return workOrderFilesResponse;
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<HistoryResponse> GetHistoryById(HistoryRequest request, ServerCallContext context)
        {
            WorkOrderModel workOrder = _workOrderApiBl.FindByName(request.WorkOrderName);
            HistoryResponse response = new HistoryResponse();
            response.Histories.AddRange(_mapper.Map<List<History>>(_historyBl.GetHistories(x => x.WorkOrderId == workOrder.Id)));

            return Task.FromResult(response);
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override async Task<UploadFileResponse> UploadFile(UploadFileRequest request, ServerCallContext context)
        {
            var response = new UploadFileResponse();
            LoggerUtil.LogDebug($"FileManagement, SaveFiles was called and started.");

            var model = _mapper.Map<FileLocationModel>(request.File);

            try
            {
                response.Message = await _fileManagerBl.SaveFiles(model) == FileSaveResult.Success ? "Success" : "Failure";
            }
            catch (Exception e)
            {
                LoggerUtil.LogError(e, string.Format("An error occurred while uploading {0}", request.File.FileName));
                throw;
            }

            return await Task.FromResult(response);
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<SetValidVersionResponse> SetValidVersion(SetValidVersionRequest request, ServerCallContext context)
        {
            WorkOrderVersion version = _workOrderVersionBl.GetOne(x => x.Name == request.VersionName);
            WorkOrderModel workOrder = _workOrderApiBl.Get(x => x.Id == version.WorkOrderId);
            version.WorkOrderName = workOrder.Name;

            SetValidVersionResponse result = new SetValidVersionResponse
            {
                Version = _mapper.Map<Olameter.WFMS.WorkOrderExecution.Common.V1.Version>(_workOrderVersionBl.SetAsValid(version, true))
            };
            return Task.FromResult(result);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<WorkOrdersAggregateResponseList> GetAggregateListByContract(ListByContractRequest request, ServerCallContext context)
        {
            WorkOrdersAggregateResponseList result = new WorkOrdersAggregateResponseList();

            result.WorkOrderAggregate.AddRange(_mapper.Map<List<WorkOrderAggregate>>(_auditBl.GetSummary(x => x.ContractName == request.ContractName)));

            return Task.FromResult(result);
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrdersResponseList> RetryFailedEtv(Empty request, ServerCallContext context)
        {
            var username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);
            WorkOrdersResponseList result = new WorkOrdersResponseList();
            result.WorkOrders.AddRange(_workOrderApiBl.RetryFailedPostProcess(username).Select(x => _mapper.Map<WorkOrder>(x)));

            return Task.FromResult(result);
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public async override Task<CalculateBlackOutResponse> CalculateBlackOut(CalculateBlackOutRequest request, ServerCallContext context)
        {
            CalculateBlackOutResponse result = new CalculateBlackOutResponse();
            DateTime date = request.CalculationDate.ToDateTime();

            try
            {
                _workOrderBlackOutBl.CalculateProcessBlackOutAsync(date, request.ContractNames.ToList());
                result.Result = true;
            }
            catch (Exception e)
            {
                var formatedDate = date.ToString("yyyy/MM/dd");
                LoggerUtil.LogError(e, string.Format("An error occurred while calculating blackout for {0}", formatedDate));
                result.Result = false;
            }

            return result;
        }


        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderSchedulerResponse> CreateScheduler(WorkOrderSchedulerRequest request, ServerCallContext context)
        {
            WorkOrderSchedulerModel model = _mapper.Map<WorkOrderSchedulerModel>(request.WorkOrderScheduler);

            return Task.FromResult(new WorkOrderSchedulerResponse
            {
                WorkOrderScheduler = _mapper.Map<WorkOrderScheduler>(_workOrderSchedulerBl.Create(model))
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderSchedulerResponse> GetScheduler(WorkOrderSchedulerNameRequest request, ServerCallContext context)
        {
            return Task.FromResult(new WorkOrderSchedulerResponse
            {
                WorkOrderScheduler = _mapper.Map<WorkOrderScheduler>(_workOrderSchedulerBl.Get(x => x.Name == request.WorkOrderSchedulerName))
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderSchedulerResponse> DeleteScheduler(WorkOrderSchedulerNameRequest request, ServerCallContext context)
        {
            return Task.FromResult(new WorkOrderSchedulerResponse
            {
                WorkOrderScheduler = _mapper.Map<WorkOrderScheduler>(_workOrderSchedulerBl.Delete(request.WorkOrderSchedulerName))
            });
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<WorkOrderSchedulerListResponse> GetSchedulerByWorkOrderAndDate(GetByWorkOrderNameDateTimeRequest request, ServerCallContext context)
        {
            WorkOrderSchedulerListResponse result = new WorkOrderSchedulerListResponse();
            result.WorkOrderSchedulers.AddRange(_mapper.Map<List<WorkOrderScheduler>>(_workOrderSchedulerBl.GetAll(x => x.AppointmentDate == request.Date.ToDateTime() && x.WorkOrder.Name == request.WorkOrderName)));

            return Task.FromResult(result);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<WorkOrderSchedulerListResponse> GetSchedulerListByWorkOrder(GetRequest request, ServerCallContext context)
        {
            WorkOrderSchedulerListResponse result = new WorkOrderSchedulerListResponse();
            result.WorkOrderSchedulers.AddRange(_mapper.Map<List<WorkOrderScheduler>>(_workOrderSchedulerBl.GetAll(x => x.WorkOrder.Name == request.WorkOrderName)));

            return Task.FromResult(result);
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderSchedulerResponse> UpdateScheduler(WorkOrderSchedulerRequest request, ServerCallContext context)
        {
            WorkOrderSchedulerModel model = _mapper.Map<WorkOrderSchedulerModel>(request.WorkOrderScheduler);

            return Task.FromResult(new WorkOrderSchedulerResponse
            {
                WorkOrderScheduler = _mapper.Map<WorkOrderScheduler>(_workOrderSchedulerBl.Update(model))
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderResponse> CancelWorkOrderAppointment(CancelWorkOrderAppointmentRequest request, ServerCallContext context)
        {
            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(_workOrderSchedulerBl.CancelWorkOrderAppointment(request.WorkOrderName))
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderResponse> CreateWorkOrderAppointment(CreateWorkOrderAppointmentRequest request, ServerCallContext context)
        {

            string username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(_workOrderApiBl.CreateWorkOrderAppointment(request.WorkOrderName, username))
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderResponse> SaveAndCreateNextTask(SaveAndCreateNextTaskRequest request, ServerCallContext context)
        {
            WorkOrderSchedulerModel woScheduler = _mapper.Map<WorkOrderSchedulerModel>(request.WorkOrderScheduler);
            WorkOrderModel workOrder = _mapper.Map<WorkOrderModel>(request.WorkOrder);
            WorkOrderTaskModel task = workOrder.Tasks.FirstOrDefault(x => x.WorkOrderTaskName == request.CurrentTaskName);

            workOrder.Tasks = null;

            string username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);

            if (task.TaskStatusString == Model.TaskStatus.TaskCompleted.ToString())
            {
                //Cancel old appointment if any
                try { _workOrderSchedulerBl.CancelWorkOrderAppointment(workOrder.Name); } catch { }

                woScheduler = _workOrderSchedulerBl.Create(woScheduler);
            }

            try
            {
                workOrder = _workOrderApiBl.UpdateAndCreateNextTask(workOrder, task, username);
            }
            catch (Exception ex)
            {
                try
                {
                    if (task.TaskStatusString == Model.TaskStatus.TaskCompleted.ToString())
                    {
                        _workOrderSchedulerBl.Delete(woScheduler.Name);
                    }
                }
                catch { }

                throw;
            }

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(workOrder)
            });
        }

        //[Authorize(Policy = CustomAuthPolicies.ForClient)] Enable it when ready
        [AllowAnonymous]
        public override Task<SaveWorkOrdersAndAppointmentResponse> SaveWorkOrdersAndAppointment(SaveWorkOrdersAndAppointmentRequest request, ServerCallContext context)
        {
            SaveWorkOrdersAndAppointmentResponse response = new SaveWorkOrdersAndAppointmentResponse();
            WorkOrderSchedulerModel woScheduler = _mapper.Map<WorkOrderSchedulerModel>(request.WorkOrderScheduler);
            List<WorkOrderModel> workOrders = _mapper.Map<List<WorkOrderModel>>(request.WorkOrders);

            if (workOrders.Count == 0 || woScheduler == null)
            {
                response.Result = false;
            }
            else
            {
                string username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);
                response.Result = _workOrderApiBl.SaveWorkOrdersAndAppointment(workOrders, woScheduler, username);
            }

            return Task.FromResult(response);
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderResponse> WorkOrderOptOut(WorkOrderOptOutRequest request, ServerCallContext context)
        {
            WorkOrderModel workOrder = _mapper.Map<WorkOrderModel>(request.WorkOrder);
            OptOutDto optOut = _mapper.Map<OptOutDto>(request.OptOut);

            workOrder.Tasks = null;


            string username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);

            //Cancel old appointment if any
            try { _workOrderSchedulerBl.CancelWorkOrderAppointment(workOrder.Name); } catch { }

            try
            {
                workOrder = _workOrderApiBl.OptOutWorkOrder(workOrder, optOut, username);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(ex, string.Format("Failed to OptOut workorder {0}", workOrder.Name));
                throw;
            }

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(workOrder)
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderResponse> WorkOrderCSRHold(WorkOrderCSRHoldRequest request, ServerCallContext context)
        {
            WorkOrderModel workOrder = _mapper.Map<WorkOrderModel>(request.WorkOrder);
            CSRHoldDto csrHold = _mapper.Map<CSRHoldDto>(request.CsrHold);

            workOrder.Tasks = null;

            //TODO: Task 49832
            string username = ServerCallContextUtils.GetClaimValue(context, _claimName);

            try
            {
                workOrder = _workOrderApiBl.CSRHoldWorkOrder(workOrder, csrHold, username);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(ex, string.Format("Failed to CSRHold workorder {0}", workOrder.Name));
                throw;
            }

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(workOrder)
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderResponse> WorkOrderReleaseCSRHold(WorkOrderReleaseCSRHoldRequest request, ServerCallContext context)
        {
            WorkOrderModel workOrder;
            //TODO: Task 49832
            string username = ServerCallContextUtils.GetClaimValue(context, _claimName);

            try
            {
                workOrder = _workOrderApiBl.ReleaseCSRHoldWorkOrder(request.WorkOrderName, username);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(ex, string.Format("Failed to release CSRHold workorder {0}", request.WorkOrderName));
                throw;
            }

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(workOrder)
            });
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<WorkOrderResponse> WorkOrderAdmin(WorkOrderAdminRequest request, ServerCallContext context)
        {
            WorkOrderModel workOrder = _mapper.Map<WorkOrderModel>(request.WorkOrder);
            WoAdminDto woAdmin = _mapper.Map<WoAdminDto>(request.WoAdmin);

            workOrder.Tasks = null;

            string username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);
            string token = context.RequestHeaders.Get("authorization").Value.Split(' ')[1];

            try
            {
                workOrder = _workOrderApiBl.AdminWorkOrder(workOrder, woAdmin, username, token);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(ex, string.Format("Failed to Admin workorder {0}", workOrder.Name));
                throw;
            }

            return Task.FromResult(new WorkOrderResponse
            {
                WorkOrder = _mapper.Map<WorkOrder>(workOrder)
            });
        }

        public override Task<WorkOrderResponse> WorkOrderAdminMassUpdate(WorkOrderAdminMassUpdateRequest request, ServerCallContext context)
        {
            List<WorkOrderModel> workOrderList = _mapper.Map<List<WorkOrderModel>>(request.WorkOrders);
            WoAdminDto woAdmin = _mapper.Map<WoAdminDto>(request.WoAdmin);

            foreach (var workOrder in workOrderList)
                workOrder.Tasks = null;

            string username = ServerCallContextUtils.GetClaimValue(context, _claimName, _awsInfo);
            string token = context.RequestHeaders.Get("authorization").Value.Split(' ')[1];

            bool result = false;

            try
            {
                result = _workOrderApiBl.AdminWorkOrderMassUpdate(workOrderList, woAdmin, username, token);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(ex, string.Format("Failed to Admin workorders Mass Update"));
                throw;
            }

            return base.WorkOrderAdminMassUpdate(request, context);
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override async Task GetWorkOrderForHelpDeskStream(GetWorkOrderForHelpDeskRequest request, IServerStreamWriter<GetWorkOrderForHelpDeskResponseStream> responseStream, ServerCallContext context)
        {
            GetWorkOrderForHelpDeskResponse response = new GetWorkOrderForHelpDeskResponse();
            TwilioSearchDto searchDto = _mapper.Map<TwilioSearchDto>(request.Request);
            IAsyncEnumerator<WorkOrderHelpdeskModel> result = _workOrderApiBl.GetByContractAndPhoneAsync(searchDto);

            while (await result.MoveNextAsync())
            {
                await responseStream.WriteAsync(new GetWorkOrderForHelpDeskResponseStream { Result = _mapper.Map<WorkOrderHelpDesk>(result.Current) });
            }
        }

        [Authorize(Policy = CustomAuthPolicies.ForWrite)]
        public override Task<GetWorkOrderForHelpDeskResponse> GetWorkOrderForHelpDesk(GetWorkOrderForHelpDeskRequest request, ServerCallContext context)
        {
            GetWorkOrderForHelpDeskResponse response = new GetWorkOrderForHelpDeskResponse();
            TwilioSearchDto searchDto = _mapper.Map<TwilioSearchDto>(request.Request);
            IEnumerable<WorkOrderHelpdeskModel> result = _workOrderApiBl.GetByContractAndPhone(searchDto);
            Stopwatch stopwatch = Stopwatch.StartNew();
            response.Result.AddRange(result.Select(x => _mapper.Map<WorkOrderHelpDesk>(x)));
            stopwatch.Stop();
            LoggerUtil.LogDebug($"Transfer Data:{stopwatch.Elapsed.TotalSeconds}");
            return Task.FromResult(response);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public async override Task DownloadFile(ListByContractRequest request, IServerStreamWriter<DownloadFileResponse> responseStream, ServerCallContext context)
        {
            OlameterFramework.OUIFramework.RequestDto filter = _mapper.Map<OlameterFramework.OUIFramework.RequestDto>(request.Filter);
            var startD = request.StartDate.ToDateTime();
            var endD = request.EndDate.ToDateTime();
            var installDateOption = InstallDateOption.Custom;
            System.Enum.TryParse<InstallDateOption>(request.InstallDateOption, out installDateOption);
            var report = await this._workOrderApiBl.GetIFSByAssignedTo(request.ContractName, startD, endD, request.WoStatusNames.ToList(), filter, installDateOption);

            byte[] byteArray = Encoding.ASCII.GetBytes(report);
            using MemoryStream stream = new(byteArray);

            var buffer = new byte[1024 * 1024]; // 1MB chunks

            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
            {
                await responseStream.WriteAsync(new DownloadFileResponse
                {
                    File = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead),
                });
            }
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<GetLayerDataByPolygonResponse> GetLayerDataByPolygon(GetLayerDataByPolygonRequest request, ServerCallContext context)
        {
            var mapped = this._mapper.Map<LayerDataByPolygonDto>(request.ContractRequest);
            mapped.Json = request.Json;
            OlameterFramework.OUIFramework.RequestDto dto = this._mapper.Map<OlameterFramework.OUIFramework.RequestDto>(request.ContractRequest.Filter);

            List<string> membersArray = new(), operatorsArray = new(), valuesArray = new();

            if (dto.IsNotNull()) ToFrameWork.ExtractFilterSP(dto.Filters, ref membersArray, ref operatorsArray, ref valuesArray);

            mapped.Members = membersArray;
            mapped.Operators = operatorsArray;
            mapped.Values = valuesArray;

            IEnumerable<string> result = this._workOrderApiBl.GetLayerDataByPolygon(mapped);

            var response = new GetLayerDataByPolygonResponse();

            response.WoNames.AddRange(result);

            return Task.FromResult(response);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<GetReportHistoryResponse> GetReportHistory(GetReportHistoryRequest request, ServerCallContext context)
        {
            var result = new GetReportHistoryResponse();
            //I have to do that because doing the join consume to much memory and time.
            WorkOrderModel workOrder = _workOrderApiBl.GetList(x => x.Name == request.WorkOrderName, noTracking: false).Single();
            List<ReportHistoryModel> reportHistories = _reportHistoryBl.GetList(x => x.WorkOrderId == workOrder.Id).ToList();
            reportHistories.ForEach(rh => rh.WorkOrder = workOrder);
            result.ReportHistory.AddRange(_mapper.Map<List<ReportHistory>>(reportHistories));
            return Task.FromResult(result);
        }

        [Authorize(Policy = AuthPolicies.ForRead)]
        public override Task<GetWorkOrderCallAttemptsResponse> GetWorkOrderCallAttempts(GetWorkOrderCallAttemptsRequest request, ServerCallContext context)
        {
            var response = new GetWorkOrderCallAttemptsResponse();

            var callAttempts = _autoDialerBl.GetCallAttempts(request.WorkOrderName);
            IEnumerable<CallAttempt> result = _mapper.Map<List<CallAttempt>>(callAttempts);

            response.CallAttempts.AddRange(result);

            return Task.FromResult(response);
        }
    }
}
