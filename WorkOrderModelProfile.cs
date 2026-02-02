using Olameter.WFMS.WorkOrderExecution.Common.V1;
using OlameterFramework.OFramework.BL;
using OlameterFramework.OFramework.ConfigUtils;
using OlameterFramework.OFramework.Utils;
using System;
using System.Linq;
using System.Text.Json;
using WFMS.WorkOrderExecution.Model.Dto;
using WFMS.WorkOrderExecution.Model.Messages;
using SyncQueryProto = Olameter.WFMS.WorkOrderExecution.SyncQuery.V1;
using ApiProto = Olameter.WFMS.WorkOrderExecution.API.V1;
using Olameter.WFMS.WorkOrderExecution.InstallDate.V1;
using System.Data;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Olameter.WFMS.WorkOrderExecution.Dispatch.V1;
using System.Collections.Generic;
using Olameter.WFMS.WorkOrderExecution.API.V1;
using System.Text.Json.Nodes;

namespace WFMS.WorkOrderExecution.Model.MappingConfigurations
{
    public class WorkOrderModelProfile : AutoMapperBase
    {
        public WorkOrderModelProfile()
        {

            CreateMap<WorkOrderInstallDateDto, WorkOrderInstallDate>()
                .ForPath(x => x.Name, y => y.MapFrom(z => z.Name))
                .ForPath(x => x.ContractName, y => y.MapFrom(z => z.ContractName))
                .ForPath(x => x.ServiceType, y => y.MapFrom(z => z.ServiceType))
                .ReverseMap()
                .ForPath(x => x.Name, y => y.MapFrom(z => z.Name))
                .ForPath(x => x.ContractName, y => y.MapFrom(z => z.ContractName))
                .ForPath(x => x.ServiceType, y => y.MapFrom(z => z.ServiceType));

            CreateMap<InstallDate, InstallDateDto>()
                .ForPath(x => x.IsAll, y => y.MapFrom(z => z.All))
                .ForPath(x => x.WorkOrders, y => y.MapFrom(z => z.WorkOrders.ToList()))
                .ForPath(x => x.Date, y => y.MapFrom(z => z.Remove ? DateTime.MinValue : z.Date.ToDateTime().Date));

            CreateMap<InstallDateResultDto, InstallDateResult>()
                .ForPath(x => x.Error, y => y.MapFrom(z => z.Error.ToList()));

            CreateMap<InstallDateError, InstallDateErrorDto>()
                .ForPath(x => x.Name, y => y.MapFrom(z => z.WorkOrderName))
                .ReverseMap()
                .ForPath(x => x.WorkOrderName, y => y.MapFrom(z => z.Name));

            CreateMap<WorkOrderModel, WorkOrderModel>()
                .ForMember(x => x.Id, y => y.Ignore())
                .ForMember(x => x.JsonData, y => y.MapFrom((src, des, member) => ManualMappers.FormatPhoneNumber(ManualMappers.ExternalJsonUpdate(src.JsonData,des.JsonData))))
                .ForMember(x => x.Versions, y => y.Ignore())
                .ForMember(x => x.PositionSRID, y => y.Ignore())
                .ForMember(x => x.Position, y => y.Ignore())
                .ForMember(x => x.Geofence, y => y.Ignore())
                .ForMember(x => x.StatusLastChange, y => y.Ignore());

            CreateMap<WorkedOrderImported, WorkOrderModel>()
                .ForMember(p => p.ContractName, opt => opt.MapFrom((src, des, member) => src.Context?.ContractId ?? string.Empty))
                .ForMember(p => p.ServiceType, opt => opt.MapFrom((src, des, member) => src.Context?.ServiceType ?? string.Empty))
                .ForMember(p => p.JsonData, opt => opt.MapFrom((src, des, member) => ManualMappers.FormatPhoneNumber(JsonDocument.Parse(src.Data.ToString()))))
                .ForPath(x => x.Active, y => y.MapFrom(z => true))
                .ForPath(x => x.PositionSRID, y => y.MapFrom(z => GISUtilities.Srid4326))
                .ForPath(x => x.CreatedOn, y => y.MapFrom(z => DateTime.UtcNow));

            CreateMap<WorkOrderModel, WorkOrder>()
                .ForPath(x => x.StatusWeb, y => y.MapFrom(z => z.StatusWebString))
                .ForPath(x => x.WorkOrderName, y => y.MapFrom(z => z.Name))
                .ForPath(x => x.AuditMode, y => y.MapFrom(z => z.AuditMode))
                .ForPath(x => x.AppointmentDate, y => y.MapFrom(z => z.AppointmentDate.ToProtoTimestamp()))
                .ForPath(x => x.InstallDate, y => y.MapFrom(z => z.InstallDate.ToProtoTimestamp()))
                .ForPath(x => x.FsrName, y => y.MapFrom(z => z.FsrName))
                .ForMember(x => x.PostProcessing, y => y.MapFrom(z => z.PostProcessStatusString))
                .ForMember(x => x.JsonData, y => y.MapFrom((source, dest, z) => ManualMappers.FormatPhoneNumber(source.JsonData).ToJsonString()))
                .ForPath(x => x.EventDate, y => y.MapFrom(z => z.StatusLastChange.ToProtoTimestamp()))
                .ForMember(x => x.ContractTimeZone, y => y.MapFrom(z => z.ContractTimeZone));

            CreateMap<WorkOrder, WorkOrderModel>()
                .ForMember(x => x.StatusWeb, y => y.Ignore())
                .ForMember(x => x.StatusWebString, y => y.MapFrom(z => z.StatusWeb))
                .ForMember(x => x.PostProcessStatusId, y => y.Ignore())
                //.ForMember(x => x.Position, y => y.MapFrom((source, dest, z) => source.Position != ByteString.Empty ? wkbReader.Read(source.Position.ToByteArray()) : null))
                .ForMember(x => x.PostProcessStatusString, y => y.MapFrom(z => z.PostProcessing))
                .ForMember(x => x.ContractName, y => y.MapFrom(z => z.ContractName))
                .ForMember(x => x.AppointmentDate, y => y.MapFrom(z => z.AppointmentDate.ToNullableDateTime()))
                .ForMember(x => x.Name, y => y.MapFrom(z => z.WorkOrderName))
                .ForMember(x => x.Tasks, y => y.MapFrom((source, dest, z) => source.Tasks?.ToList()))
                .ForMember(x => x.Versions, y => y.MapFrom((source, dest, z) => source.Versions?.ToList()))
                .ForMember(x => x.FsrName, y => y.MapFrom(z => z.FsrName))
                .ForMember(x => x.CreatedOn, y => y.MapFrom(z => z.CreatedOn != null ? z.CreatedOn.ToDateTime() : DateTime.UtcNow))
                .ForMember(x => x.StartDateBlackout, y => y.MapFrom(z => z.StartDateBlackout.ToNullableDateTime()))
                .ForMember(x => x.EndDateBlackout, y => y.MapFrom(z => z.EndDateBlackout.ToNullableDateTime()))
                .ForMember(x => x.InstallDate, y => y.MapFrom(z => z.InstallDate.ToNullableDateTime()))
                .ForMember(x => x.StatusWeb, y => y.MapFrom((source, dest, z) => !source.StatusWeb.IsNullOrEmpty() ? new GenericEnumValues.Model.GenericEnumValue(StatusWeb.None.GetEnumName(), source.StatusWeb, null, null, null) : new GenericEnumValues.Model.GenericEnumValue(StatusWeb.None.GetEnumName(), StatusWeb.None.ToStringValue(), null, null, null)))
                .ForMember(x => x.JsonData, y => y.MapFrom((source, dest, z) => ManualMappers.FormatPhoneNumber(JsonDocument.Parse(source.JsonData))));

            CreateMap<WorkOrderModel, WorkOrderReadyForExportIntegrationEventModel>()
               .ForMember(x => x.WorkOrderId, y => y.MapFrom(z => z.Name))
               .ForMember(x => x.Model, y => y.MapFrom(z => z));

            CreateMap<WorkOrderModel, WorkOrderReadyForExportModel>();

            CreateMap<WorkOrderAuditSummary, WorkOrderAggregate>();
            CreateMap<WorkOrderStatistic, SyncQueryProto.WorkOrderStatistic>().ReverseMap();
            CreateMap<WorkOrderModel,WorkOrderHelpdeskModel>().ReverseMap();
            CreateMap<ApiProto.WorkOrderHelpDesk, WorkOrderHelpdeskModel>()
                .ForPath(x => x.PlannedInstallDate, y => y.MapFrom(z => z.PlannedInstallDate.ToNullableDateTime()))
                .ForPath(x => x.PhoneNumber, y => y.MapFrom(z => z.Phone))
                .ReverseMap()
                .ForPath(x => x.PlannedInstallDate, y => y.MapFrom(z => z.PlannedInstallDate.ToProtoTimestamp()))
                .ForPath(x => x.Phone, y => y.MapFrom(z => z.PhoneNumber));
            CreateMap<ApiProto.HelpDeskData, TwilioSearchDto>()

                .ForPath(x => x.PlannedInstallDate, y => y.MapFrom(z => z.PlannedInstallDate.ToNullableDateTime()))
                .ForPath(x => x.CellPhoneNo, y => y.MapFrom(z => z.Phone))
                .ReverseMap()
                .ForPath(x => x.PlannedInstallDate, y => y.MapFrom(z => z.PlannedInstallDate.ToProtoTimestamp()))
                .ForPath(x => x.Phone, y => y.MapFrom(z => z.CellPhoneNo));

            CreateMap<ApiProto.ListByContractRequest, LayerDataByPolygonDto>()
                .ForMember(x => x.ContractName, y => y.MapFrom(z => z.ContractName))
                .ForMember(x => x.InstallDateOption, y => y.MapFrom(z => z.InstallDateOption))
                .ForMember(x => x.StartDate, y => y.MapFrom(z => z.StartDate.ToDateTime()))
                .ForMember(x => x.EndDate, y => y.MapFrom(z => z.EndDate.ToDateTime()))
                .ForMember(x => x.Members, y => y.Ignore())
                .ForMember(x => x.Operators, y => y.Ignore())
                .ForMember(x => x.Values, y => y.Ignore())
                .ForMember(x => x.WoStatusNames, y => y.MapFrom(z => z.WoStatusNames));

            CreateMap<WorkOrderModel, UpdatedWorkOrderDto>()
                .ForMember(x => x.StatusWeb, y => y.MapFrom(z => z.StatusWebString))
                .ForMember(x => x.WorkOrderName, y => y.MapFrom(z => z.Name))
                .ForMember(x => x.TeamAssignTo, y => y.MapFrom(z => z.TeamDispatch != null ? z.TeamDispatch.Title : null))
                .ForMember(x => x.Techs, y => y.MapFrom(z => ManualMappers.MapTechs(z)));

            CreateMap<UpdatedWorkOrderDto, UpdatedWorkOrder>()
                .ForMember(x => x.StatusWeb, y => y.MapFrom(z => z.StatusWeb))
                .ForMember(x => x.WorkOrderName, y => y.MapFrom(z => z.WorkOrderName))
                .ForMember(x => x.TeamAssignTo, y => y.MapFrom(z => z.TeamAssignTo))
                .ForMember(x => x.Techs, y => y.MapFrom(z => z.Techs));

            CreateMap<Tech, TechDto>()
                .ForMember(x => x.FsrName, y => y.MapFrom(z => z.FsrName))
                .ForMember(x => x.AssignedTo, y => y.MapFrom(z => z.AssignTo))
                .ReverseMap();

            CreateMap<FsrCount, FsrCountDto>()
                .ForMember(x => x.FsrName, y => y.MapFrom(z => z.FsrName))
                .ForMember(x => x.StatusWeb, y => y.MapFrom(z => z.StatusWeb))
                .ForMember(x => x.Count, y => y.MapFrom(z => z.Count))
                .ReverseMap();

            CreateMap<TeamCount, TeamCountDto>()
                .ForMember(x => x.TeamId, y => y.MapFrom(z => z.TeamId))
                .ForMember(x => x.TeamTitle, y => y.MapFrom(z => z.TeamTitle))
                .ForMember(x => x.StatusWeb, y => y.MapFrom(z => z.StatusWeb))
                .ForMember(x => x.Count, y => y.MapFrom(z => z.Count))
                .ForMember(x => x.Techs, y => y.MapFrom(z => z.Techs))
                .ReverseMap();

            CreateMap<WorkOrderModel, WorkOrderStatusChangeDto>()
                .ForMember(x => x.WorkOrderName, y => y.MapFrom(z => z.Name))
                .ForMember(x => x.WorkOrderStatus, y => y.MapFrom(z => z.StatusWeb.Name))
                .ForMember(x => x.TaskName, y => y.MapFrom(z => z.Tasks.OrderBy(t => t.Id).LastOrDefault().Name))
                .ForMember(x => x.TaskStatus, y => y.MapFrom(z => z.Tasks.OrderBy(t => t.Id).LastOrDefault().TaskStatus.Name));

            CreateMap<OptOut, OptOutDto>().ReverseMap();

            CreateMap<CSRHold, CSRHoldDto>().ReverseMap();

            CreateMap<WoAdmin, WoAdminDto>()
                .ForMember(x => x.NextTaskToCreate, y => y.MapFrom(z => z.TaskToCreate)).ReverseMap();

        }
}
public static class DateTimeExtension
{
        public static Google.Protobuf.WellKnownTypes.Timestamp ToProtoTimestamp(this DateTime? date)
        {

            if (date.HasValue) {
                if (date.Value.Kind != DateTimeKind.Utc)
                    date = new DateTime(date.Value.Ticks, DateTimeKind.Utc);

                return Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(date.Value); 
            }

            var timestamp = new Google.Protobuf.WellKnownTypes.Timestamp();
            timestamp.Seconds = 0;

            return timestamp;
        }

        public static Google.Protobuf.WellKnownTypes.Timestamp ToProtoTimestamp(this DateTimeOffset? date)
        {

            if (date.HasValue)
            {
                return ((DateTime?)date.Value.UtcDateTime).ToProtoTimestamp();
            }

            var timestamp = new Google.Protobuf.WellKnownTypes.Timestamp();
            timestamp.Seconds = 0;

            return timestamp;
        }

        public static DateTime? ToNullableDateTime(this Google.Protobuf.WellKnownTypes.Timestamp timeStamp)
        {
            DateTime? dateTime = null;

            if (timeStamp != null)
            {
                var tmp = timeStamp.ToDateTime();

                if(tmp.Year > 1970)
                {
                    dateTime = tmp; 
                }
            }

            return dateTime;
        }
    }

    public static class ManualMappers
    {
        public static List<TechDto> MapTechs(WorkOrderModel wo)
        {
            var techs = new List<TechDto>();

            if (!string.IsNullOrEmpty(wo.FsrName))
            {
                techs.Add(new TechDto { FsrName = wo.FsrName, AssignedTo = wo.AssignedTo });
            }
            else
            {
                if (wo.TeamDispatch?.TeamDispatchTechs != null)
                {
                    foreach (var tech in wo.TeamDispatch.TeamDispatchTechs)
                    {
                        techs.Add(new TechDto { FsrName = tech.FsrName, AssignedTo = tech.AssignedTo });
                    }
                }                
            }

            return techs;
        }

        public static WorkOrderModel Map(IDataRecord row)
        {
            //note: same return on ola_filter in the c# Map funtion
            var workOrder = new WorkOrderModel();

            MapColumnValue(row, workOrder, nameof(workOrder.Id));
            MapColumnValue(row, workOrder, nameof(workOrder.ContractName));
            //TODO Revise if we need this
            //MapColumnValue(row, workOrder, nameof(workOrder.PositionSRID));
            MapColumnValue(row, workOrder, nameof(workOrder.JsonData));
            MapColumnValue(row, workOrder, nameof(workOrder.FsrName));

            long statusWebId = 0;

            //TODO Revise if we need this
            //MapColumnValue(row, workOrder, nameof(workOrder.StatusWebId));

            //TODO Revise if we need this
            // include this table to improve performance
            var tmp = GetColumnValue(row, "StatusWebName"/*hardcoden in PG ola_get_work_orders_contract funct*/);

            if (tmp != null)
            {
                try { workOrder.StatusWeb = new GenericEnumValues.Model.GenericEnumValue { Id = statusWebId, Name = Convert.ToString(tmp) }; } catch (Exception e) { LoggerUtil.LogWarning($"StatusWeb: {e.Message}"); }
            }

            MapColumnValue(row, workOrder, nameof(workOrder.Name));
            MapColumnValue(row, workOrder, nameof(workOrder.ServiceType));
            MapColumnValue(row, workOrder, nameof(workOrder.Active));
            MapColumnValue(row, workOrder, nameof(workOrder.Audited));
            MapColumnValue(row, workOrder, nameof(workOrder.AuditorName));
            MapColumnValue(row, workOrder, nameof(workOrder.CreatedOn));
            MapColumnValue(row, workOrder, nameof(workOrder.AssignedTo));
            MapColumnValue(row, workOrder, nameof(workOrder.AuditMode));
            MapColumnValue(row, workOrder, nameof(workOrder.PostProcessStatusId));
            MapColumnValue(row, workOrder, nameof(workOrder.BlackoutRemainingDays));
            MapColumnValue(row, workOrder, nameof(workOrder.StartDateBlackout));
            MapColumnValue(row, workOrder, nameof(workOrder.AppointmentDate));
            MapColumnValue(row, workOrder, nameof(workOrder.EndDateBlackout));
            MapColumnValue(row, workOrder, nameof(workOrder.InstallDate));
            MapColumnValue(row, workOrder, nameof(workOrder.StatusLastChange));
            MapColumnValue(row, workOrder, nameof(workOrder.MexFlag));

            return workOrder;
        }

        private static object GetColumnValue(IDataRecord row, string columnName)
        {
            int index;

            try
            {
                index = row.GetOrdinal(columnName);  
            }
            catch (Exception e)
            {
                LoggerUtil.LogWarning($"{columnName}: {e.Message}");
                return null;
            }


            if (row.IsDBNull(index))
            {
                return null;
            }

            return row.GetValue(index);
        }

        public static void MapColumnValue(IDataRecord row, WorkOrderModel workOrder, string columnName)
        {
            int index;

            try
            {
                index = row.GetOrdinal(columnName);
            }
            catch (Exception e)
            {
                LoggerUtil.LogWarning($"{columnName}: {e.Message}");
                return;
            }            

            if (row.IsDBNull(index))
            {
                return;
            }

            PropertyInfo propInfo;

            try
            {
                Type type = workOrder.GetType();
                propInfo = type.GetProperty(columnName);
            }
            catch (Exception e)
            {
                LoggerUtil.LogWarning($"{columnName}: {e.Message}");
                return;
            }

            if(propInfo.PropertyType == typeof(Int32))
            {
                try { propInfo.SetValue(workOrder, Convert.ToInt32(row.GetValue(index))); } catch (Exception e) { LoggerUtil.LogWarning($"Id: {e.Message}"); }
            }
            else if (propInfo.PropertyType == typeof(Int64))
            {
                try { propInfo.SetValue(workOrder, Convert.ToInt64(row.GetValue(index))); } catch (Exception e) { LoggerUtil.LogWarning($"Id: {e.Message}"); }
            }
            else if (propInfo.PropertyType == typeof(String))
            {
                try { propInfo.SetValue(workOrder, Convert.ToString(row.GetValue(index))); } catch (Exception e) { LoggerUtil.LogWarning($"Id: {e.Message}"); }
            }
            else if (propInfo.PropertyType == typeof(Boolean))
            {
                try { propInfo.SetValue(workOrder, Convert.ToBoolean(row.GetValue(index))); } catch (Exception e) { LoggerUtil.LogWarning($"Id: {e.Message}"); }
            }
            else if (propInfo.PropertyType == typeof(DateTime) || propInfo.PropertyType == typeof(Nullable<DateTime>))
            {
                try { propInfo.SetValue(workOrder, Convert.ToDateTime(row.GetValue(index))); } catch (Exception e) { LoggerUtil.LogWarning($"Id: {e.Message}"); }
            }
            else if (propInfo.PropertyType == typeof(DateTimeOffset) || propInfo.PropertyType == typeof(Nullable<DateTimeOffset>))
            {
                try { propInfo.SetValue(workOrder, new DateTimeOffset(Convert.ToDateTime(row.GetValue(index)))); } catch (Exception e) { LoggerUtil.LogWarning($"Id: {e.Message}"); }
            }
            else if (propInfo.PropertyType == typeof(JsonDocument))
            {
                try { propInfo.SetValue(workOrder, JsonDocument.Parse(Convert.ToString(row.GetValue(index)))); } catch (Exception e) { LoggerUtil.LogWarning($"Id: {e.Message}"); }
            }
            else
            {
                LoggerUtil.LogWarning($"{columnName}: No mapping for type '{propInfo.PropertyType.Name}'");
            }
        }

        public static JsonDocument FormatPhoneNumber(JsonDocument src)
        {
            JsonNode source = JsonNode.Parse(src.ToJsonString());

            if (source == null || source["Contact"]?["CellPhoneNo"] == null)
            {
                return src;
            }

            try
            {
                var phone = source["Contact"]["CellPhoneNo"].GetValue<string>();
                source["Contact"]["CellPhoneNo"] = phone.Replace("-", "");
            }
            catch
            {
                return src;
            }            

            return JsonDocument.Parse(source.ToString());
        }

        public static JsonDocument ExternalJsonUpdate(JsonDocument src, JsonDocument des)
        {
            JToken source = JToken.Parse(src.ToJsonString());
            JToken destination = JToken.Parse(des.ToJsonString());
            UpdateJson(source, ref destination);
            return JsonDocument.Parse(destination.ToString());
        }

        private static void UpdateJson(JToken source, ref JToken destination)
        {
            if (source.Type != JTokenType.Object || destination?.Type != JTokenType.Object)
            {
                destination = source;
                return;
            }

            JObject obj1 = (JObject)source;
            JObject obj2 = (JObject)destination;

            foreach (JProperty prop1 in obj1.Properties())
            {
                JProperty prop2 = obj2.Property(prop1.Name);
                var value = prop2?.Value;
                if (prop2 == null)
                {
                    (destination as JObject).Add(new JProperty(prop1));
                }
                else
                {
                    UpdateJson(prop1.Value, ref value);
                    prop2.Value = value;
                }
            }
        }
    }
}
