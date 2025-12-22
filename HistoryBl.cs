using OlameterFramework.OFramework.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapper;
using WFMS.WorkOrderExecution.DAL;
using WFMS.WorkOrderExecution.Model;
using WFMS.WorkOrderExecution.Model.Messages;
using WFMS.WorkOrderExecution.Model.Dto;
using WFMS.WorkOrderExecution.BL.Utility;

namespace WFMS.WorkOrderExecution.BL.BusinessLayer
{
    public enum HistoryType
    {
        WOCreated,
        WOUpdated,
        WODispatched,
        WOOnHold,
        WOReadyForDispatch,
        WORecallRequested
    }

    public interface IHistoryBl
    {
        HistoryModel Create(HistoryModel entity, bool andSave = true);

        HistoryModel Create(WorkOrderModel order, HistoryType type, string username, bool andSave = true);

        HistoryModel Create(WorkOrderModel model, WorkOrderModel oldmodel, HistoryType type, string username,
            bool andSave = false);

        HistoryModel Create(WorkOrderModel order, List<AuditLogModel> audits, HistoryType type, string username, bool andSave = true, DateTime? timeStamp = null);

        HistoryModel Create(AutoDialerModel autoDialer, bool andSave = true);

        HistoryModel Create(WorkOrderModel model, PositionDto position, string username, bool andSave = true);

        IQueryable<HistoryModel> GetIQueryable(Expression<Func<HistoryModel, bool>> predicate = null);

        IQueryable<HistoryModel> GetHistories(Expression<Func<HistoryModel, bool>> predicate);

        bool SetAdAt(HistoryType type, out string ad, out string at);

    }
    public class HistoryBl : OlameterFramework.OFramework.BL.BaseCoreEntityBl<HistoryModel, HistoryDal>, IHistoryBl
    {
        public readonly IWoGenericEnumValuesBl _gevBl;
        private readonly IAuditLogBl _auditBl;
        private readonly IHistoryDal _historyDal;
        private readonly IMapper _mapper;
        private readonly IKafkaHelper _kafkaHelper;
        public readonly string AutoDialerServiceName = "Call Center";

        public HistoryBl(ApplicationDbContext context, IWoGenericEnumValuesBl gevBl, IAuditLogBl auditBl
                        , IHistoryDal dal,IMapper mapper, IKafkaHelper kafkaHelper) : base(context)
        {
            _gevBl = gevBl;
            _auditBl = auditBl;
            _historyDal = dal;
            _mapper = mapper;
            _kafkaHelper = kafkaHelper;
        }

        public IQueryable<HistoryModel> GetIQueryable(Expression<Func<HistoryModel, bool>> predicate = null)
        {
            return _dal.GetAllIQueryable(predicate, new List<Expression<Func<HistoryModel, object>>> { x => x.Audits, x => x.ActivityDetail, x => x.ActivityType, x => x.PostProcessing });
        }

        public IQueryable<HistoryModel> GetHistories(Expression<Func<HistoryModel, bool>> predicate)
        {
            return GetIQueryable(predicate).OrderByDescending(x => x.TimeStamp);
        }

        public override List<HistoryModel> Create(IEnumerable<HistoryModel> entities, bool andSave = true)
        {
            List<HistoryModel> histories = new List<HistoryModel>();
            int max = entities.Count() - 1;
            for (int i = 0; i <= max; i++)
            {
                if (i == max)
                    histories.Add(Create(entities.ToArray()[i], andSave));
                else
                    histories.Add(Create(entities.ToArray()[i], false));

            }

            return histories;
        }

        public new HistoryModel Create(HistoryModel entity, bool andSave = true)
        {
            entity.ActivityDetail = _gevBl.GetGenericEnumValue(entity.ActivityDetailString, ActivityDetail.ActivityDetailNone.GetEnumName(), andSave);
            entity.ActivityType = _gevBl.GetGenericEnumValue(entity.ActivityTypeString, ActivityType.ActivityTypeNone.GetEnumName(), andSave);
            entity.PostProcessing = _gevBl.GetGenericEnumValue(entity.PostProcessingString, PostProcessStatus.PostProcessingNone.GetEnumName(), andSave);

            Validations(entity);
            HistoryEventModel messageModel = _mapper.Map<HistoryEventModel>(entity);
            entity.Id = 0L;
            entity.WorkOrderId = entity.WorkOrderId == 0 ? entity.WorkOrder.Id : entity.WorkOrderId;
            entity.WorkOrder = null;
            entity = _historyDal.Create(entity, andSave);            
            _kafkaHelper.GenerateAndPublishHistoryMessagesAsync(messageModel);
            return entity;
        }

        public HistoryModel Create(AutoDialerModel autoDialer, bool andSave = true)
        {
            HistoryModel history = new()
            {
                WorkOrderId = autoDialer.WorkOrderId,
                WorkOrder = autoDialer.WorkOrder,
                TimeStamp = DateTime.UtcNow,
                UserName = AutoDialerServiceName,
                Audits = _auditBl.CreateAuditList(autoDialer),
                ActivityDetailString = ActivityDetail.CallAttempt.ToStringValue(),
                ActivityTypeString = ActivityType.DUD.ToStringValue(),
                PostProcessingString = PostProcessStatus.PostProcessingNone.ToStringValue()
            };

            history = this.Create(history, andSave);
            return history;
        }

        public HistoryModel Create(WorkOrderModel model, PositionDto position, string username, bool andSave = true)
        {
            HistoryModel history = new()
            {
                WorkOrderId = model.Id,
                WorkOrder = model,
                TimeStamp = DateTime.UtcNow,
                UserName = username,
                Audits = _auditBl.CreateAuditList(position),
                ActivityDetailString = ActivityDetail.CorrectPositionPostProcess.ToStringValue(),
                ActivityTypeString = ActivityType.PostProcess.ToStringValue(),
                PostProcessingString = PostProcessStatus.PostProcessingNone.ToStringValue()
            };

            history = this.Create(history, andSave);
            return history;
        }

        public HistoryModel Create(WorkOrderModel model, HistoryType type, string username, bool andSave = false)
        {
            string ad;
            string at;

            if (!SetAdAt(type, out ad, out at)) return null;

            return this.InnerCreate(model, ad, at, username, andSave);
        }

        public HistoryModel Create(WorkOrderModel model,WorkOrderModel oldModel, HistoryType type, string username, bool andSave = false)
        {
            string ad;
            string at;

            if (!SetAdAt(type, out ad, out at)) return null;

            return this.InnerCreate(model,_auditBl.CreateAuditList(model,oldModel), ad, at, username, andSave);
        }

        public HistoryModel Create(WorkOrderModel model, List<AuditLogModel> audits, HistoryType type, string username, bool andSave = true, DateTime? timeStamp = null)
        {
            string ad;
            string at;

            if (!SetAdAt(type, out ad, out at)) return null;

            return this.InnerCreate(model, audits, ad, at, username, andSave, timeStamp);
        }

        public HistoryModel Create(WorkOrderModel model, WorkOrderModel previous, string username, bool andSave = false)
        {
            //To prevent circular reference in history
            model.Tasks.ForEach(t =>
            {
                t.WorkOrder = null;
            });

            HistoryModel history = new()
            {
                WorkOrderId = model.Id,
                WorkOrder = model,
                TimeStamp = DateTime.UtcNow,
                UserName = username,
                Audits = _auditBl.CreateAuditList(model,previous),
                ActivityDetailString = "Workorderupdated",
                ActivityTypeString = ActivityType.ActivityTypeData.ToStringValue(),
                PostProcessingString = PostProcessStatus.PostProcessingNone.ToStringValue()
            };

            //Put back reference
            model.Tasks.ForEach(t =>
            {
                t.WorkOrder = model;
            });

            history = this.Create(history, andSave);
            return history;
        }

        public bool SetAdAt(HistoryType type, out string ad, out string at)
        {
            ad = null;
            at = ActivityType.ActivityTypeData.ToStringValue();
            switch (type)
            {
                case HistoryType.WOCreated:
                    ad = "Workordercreated";
                    break;
                case HistoryType.WOUpdated:
                    ad = "Workorderupdated";
                    break;
                case HistoryType.WODispatched:
                    ad = ActivityDetailConstants.WorkOrderDispatched;
                    at = ActivityTypeConstants.StatusChanged;
                    break;
                case HistoryType.WOOnHold:
                    ad = ActivityDetailConstants.WorkOrderOnHold;
                    at = ActivityTypeConstants.StatusChanged;
                    break;
                case HistoryType.WOReadyForDispatch:
                    ad = ActivityDetailConstants.WorkOrderReadyForDispatch;
                    at = ActivityTypeConstants.StatusChanged;
                    break;
                case HistoryType.WORecallRequested:
                    ad = ActivityDetailConstants.WorkOrderSetToRecallRequested;
                    at = ActivityTypeConstants.StatusChanged;
                    break;
                default: return false;
            }

            return true;
        }

        private HistoryModel InnerCreate(WorkOrderModel model, string ad, string at, string username, bool andSave = false)
        {
            return InnerCreate(model, _auditBl.CreateAuditList(model), ad, at, username, andSave);
        }

        private HistoryModel InnerCreate(WorkOrderModel model, List<AuditLogModel> audits, string ad, string at, string username, bool andSave = false, DateTime? timeStamp = null)
        {
            //To prevent circular reference in history
            model.Tasks.ForEach(t =>
            {
                t.WorkOrder = null;
            });

            HistoryModel history = new()
            {
                WorkOrderId = model.Id,
                WorkOrder = model,
                TimeStamp = timeStamp.HasValue ? timeStamp.Value : DateTime.UtcNow,
                UserName = username,
                Audits = audits,
                ActivityDetailString = ad,
                ActivityTypeString = at,
                PostProcessingString = PostProcessStatus.PostProcessingNone.ToStringValue()
            };

            //Put back reference
            model.Tasks.ForEach(t =>
            {
                t.WorkOrder = model;
            });

            history = this.Create(history, andSave);
            return history;
        }
    }
}
