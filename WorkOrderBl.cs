using AutoMapper;
using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OlameterFramework.EventBus;
using OlameterFramework.EventBus.IntegrationEventLog;
using OlameterFramework.OFramework.ConfigUtils;
using OlameterFramework.OFramework.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WFMS.WorkOrderExecution.BL.IntegrationEvents.Events;
using WFMS.WorkOrderExecution.BL.Utility;
using WFMS.WorkOrderExecution.DAL;
using WFMS.WorkOrderExecution.Model;
using WFMS.WorkOrderExecution.Model.Dto;
using WFMS.WorkOrderManager.Executor.Services;
using ExecutorModel = WFMS.WorkOrderManager.Executor.Models;
using Geometry = NetTopologySuite.Geometries.Geometry;
using Point = NetTopologySuite.Geometries.Point;
using TaskStatus = WFMS.WorkOrderExecution.Model.TaskStatus;
using System.Runtime.CompilerServices;
using GenericEnumValues.Model;
using Olameter.WFMS.WorkOrderExecution.API.V1;
using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Concurrency;
using Olameter.WFMS.WorkOrderExecution.Scheduler.V1;

[assembly: InternalsVisibleTo("WFMS.WorkOrderExecution.Tests")]
namespace WFMS.WorkOrderExecution.BL.BusinessLayer
{
    public interface IWorkOrderBl
    {
        ApplicationDbContext Context { get; }

        WorkOrderModel Create(WorkOrderModel workOrder, string username, bool andSave = false);
        void CreateFirstTask(string workOrderName, string username, WorkOrderConfigDto woConfig = null);
        List<WorkOrderModel> CreateList(List<WorkOrderModel> workOrders, string username, bool andSave = false);
        void CreateNextTask(string workOrderName, WorkOrderTaskModel currentTask, string username);
        WorkOrderModel Delete(WorkOrderModel workOrder, string username, bool andSave = false, bool inTrans = true);
        List<WorkOrderModel> DeleteList(List<WorkOrderModel> workOrders, bool andSave = false);
        bool Exist(WorkOrderModel workOrder);
        WorkOrderModel FindByName(string name);
        WorkOrderModel Get(Expression<Func<WorkOrderModel, bool>> expressionWhere);
        IQueryable<FsrCountDto> GetActiveFsrCount(string contract, IEnumerable<string> fsrList);
        IQueryable<TeamCountDto> GetActiveTeamCount(string contract, IEnumerable<int> teamList);
        List<WorkOrderModel> GetAll(string contract, DateTime startD, DateTime endD, List<string> woStatusNames, OlameterFramework.OUIFramework.RequestDto filter, InstallDateOption installDateOption = InstallDateOption.Custom);

        IAsyncEnumerable<WorkOrderModel> GetAllAsync(string contract, DateTime startD, DateTime endD, List<string> woStatusNames, OlameterFramework.OUIFramework.RequestDto filter, InstallDateOption installDateOption = InstallDateOption.Custom);
        IEnumerable<WorkOrderHelpdeskModel> GetByContractAndPhone(TwilioSearchDto search);
        IAsyncEnumerator<WorkOrderHelpdeskModel> GetByContractAndPhoneAsync(TwilioSearchDto search);
        IQueryable<FsrCountDto> GetFsrCount(string contract, IEnumerable<string> fsrList);
        IQueryable<TeamCountDto> GetTeamCount(string contract, IEnumerable<int> teamList);
        WorkOrderModel GetByName(string woName, bool includeTasks = false, bool noTracking = false);
        WorkOrderModel GetByName(string contractName, string workOrderName, bool noTracking = false);
        WorkOrderModel GetFirst(Expression<Func<WorkOrderModel, bool>> expressionWhere);
        IQueryable<WorkOrderModel> GetList(Expression<Func<WorkOrderModel, bool>> expressionWhere = null, bool noTracking = true);
        IEnumerable<string> GetLayerDataByPolygon(LayerDataByPolygonDto layerDataByPolygonDto);
        IAsyncEnumerable<WorkOrderModel> GetListAsync(Expression<Func<WorkOrderModel, bool>> expressionWhere);
        IQueryable<WorkOrderModel> GetListWithoutVersion(Expression<Func<WorkOrderModel, bool>> expressionWhere = null);
        long GetId(WorkOrderModel workOrder);
        WorkOrderModel GetOne(Expression<Func<WorkOrderModel, bool>> expressionWhere);
        List<string> GetPreFilterColumnValues(string contractName, DateTime startD, DateTime endD, string columnName, string columnFilterValue, InstallDateOption installDateOption = InstallDateOption.Custom);
        long GetTotalCount(string contractName, DateTime startD, DateTime endD, List<string> list, OlameterFramework.OUIFramework.RequestDto filter, InstallDateOption installDateOption = InstallDateOption.Custom);
        WorkOrderModel GetWithTasks(Expression<Func<WorkOrderModel, bool>> expressionWhere);
        void QueryPosition(WorkOrderModel model);
        List<WorkOrderModel> RetryFailedPostProcess(string username);
        List<WorkOrderModel> SaveFromManuelSync(List<WorkOrderModel> workOrders, string username, bool andSave = false,string token=null);
        WorkOrderModel Update(WorkOrderModel workOrder, string username, bool andSave = false, bool inTrans = true, bool isAdmin = false);
        WorkOrderModel UpdateAndCreateNextTask(WorkOrderModel workOrder, WorkOrderTaskModel currentTask, string username, bool inTrans = true);
        WorkOrderModel UpdateFromManuelSync(WorkOrderModel workOrder, WorkOrderModel existing, string username, bool andSave = false,string token =null);
        WorkOrderModel InnerUpdate(WorkOrderModel workOrder, string username, bool andSave = false, WorkOrderModel existing = null, bool isAdmin = false);
        WorkOrderModel OptOutWorkOrder(WorkOrderModel workOrder, OptOutDto optOut, string username);
        void UpdatePosition(string name, GeoCodeResultEventModel geocodeResult);
        IEnumerable<UpdatedWorkOrderDto> GetUpdatedWorkOrders(IEnumerable<string> workOrders);
        Task<string> GetIFSByAssignedTo(string contract, DateTime startD, DateTime endD, List<string> woStatusNames, OlameterFramework.OUIFramework.RequestDto filter, InstallDateOption installDateOption = InstallDateOption.Custom);
        string GetRequestedFieldsOnly(List<string> requestedFields, string jsonData);
        WorkOrderModel CreateWorkOrderAppointment(string workOrderName, string username);
        WorkOrderModel CSRHoldWorkOrder(WorkOrderModel workOrder, CSRHoldDto optOut, string username);
        WorkOrderModel ReleaseCSRHoldWorkOrder(string workOrderName, string username);
        WorkOrderTaskModel GetFirstTaskTemplate(long workOrderId);
        WorkOrderModel AdminWorkOrder(WorkOrderModel workOrder, WoAdminDto woAdmin, string username, string token = null);
        WorkOrderModel UpdateFromEvent(WorkOrderModel workOrder, string username, bool andSave = false, DateTimeOffset? eventDate = null);
        List<WorkOrderModel> StatusUpdateWorkOrder(List<WorkOrderModel> workOrders, StatusUpdateDto statusUpdate, string username, bool isAdmin = false);
        bool SaveWorkOrdersAndAppointment(List<WorkOrderModel> workOrders, WorkOrderSchedulerModel woScheduler, string username);
    }

    public class WorkOrderBl : OlameterFramework.OFramework.BL.BaseCoreEntityBl<WorkOrderModel, WorkOrderDal>, IWorkOrderBl
    {
        private readonly IMapper _mapper;
        public readonly IWoGenericEnumValuesBl _gevBl;
        public readonly IContractBl _contractBl;
        public readonly IHistoryBl _historyBl;
        public readonly IAuditLogBl _auditBl;
        public readonly IWorkOrderVersionBl _versionBl;
        private readonly IWorkOrderBlValidations _woValidations;
        private readonly IWorkOrderDal _workOrderDal;
        private readonly IGeometryHelper _geometryHelper;
        private readonly IPostProcessingBl _postProcessingBl;
        private readonly List<Expression<Func<WorkOrderModel, object>>> includeExpressions;
        private readonly IMessageIntegrationEventLogService _eventLogService;
        private readonly int _geofenceRadius;
        private ApplicationDbContext _dbContext;
        private const int DefaultRadius = 50;
        private readonly IExecutorService _executorService;
        private readonly IWorkOrderTaskDal _workOrderTaskDal;
        private readonly IWorkOrderConfig _workOrderConfig;
        private readonly IContractImportedFieldBl _contractImportedFieldBl;
        private readonly IWorkOrderStatusRules _woStatusRules;
        private readonly IWorkOrderStatusManager _workOrderStatusManager;
        private readonly IWorkOrderBlackOutBl _workOrderBlackOutBl;
        private readonly UserBl _userBl;
        private readonly IKafkaHelper _kafkaHelper;
        private readonly IServiceProvider _serviceProvider;

        public ApplicationDbContext Context { get { return _dbContext; } }

        //TODO:Very Bad needed for the unit test
        public IWorkOrderTaskBl TaskBl { get; set; }

        public WorkOrderBl(ApplicationDbContext dbContext, IWorkOrderVersionBl versionBl,
            IHistoryBl historyBl, IAuditLogBl auditBl, IContractBl contractBl,
            IWoGenericEnumValuesBl gevBl, IPostProcessingBl postProcessingBl,
            IMapper mapper,
            IMessageIntegrationEventLogService eventLogService, IWorkOrderBlValidations woValidations,
            IWorkOrderDal workOrderDal, IGeometryHelper geometryHelper,
            IExecutorService executorService, IWorkOrderTaskDal workOrderTaskDal,
            IWorkOrderConfig workOrderConfig,
            IContractImportedFieldBl contractImportedFieldBl,
            IWorkOrderStatusRules workOrderStatusRule,
            IWorkOrderStatusManager workOrderStatusManager,
            IWorkOrderBlackOutBl workOrderBlackOutBl,
            UserBl userBl, IKafkaHelper kafkaHelper,
            IServiceProvider serviceProvider) : base(dbContext)
        {
            _dbContext = dbContext;
            if (!int.TryParse(ConfigUtil.GetValue("geofenceradius"), out _geofenceRadius))
                _geofenceRadius = DefaultRadius;
            _contractBl = contractBl;
            _gevBl = gevBl;
            _postProcessingBl = postProcessingBl;
            _mapper = mapper;
            _auditBl = auditBl;
            _historyBl = historyBl;
            _eventLogService = eventLogService;
            _versionBl = versionBl;
            _woValidations = woValidations;
            _workOrderDal = workOrderDal;
            _geometryHelper = geometryHelper;
            includeExpressions = new List<Expression<Func<WorkOrderModel, object>>> {
                x=>x.StatusWeb,
                x => x.Versions
            };

            _executorService = executorService;
            _workOrderTaskDal = workOrderTaskDal;
            _workOrderConfig = workOrderConfig;
            _contractImportedFieldBl = contractImportedFieldBl;
            _woStatusRules = workOrderStatusRule;
            _workOrderStatusManager = workOrderStatusManager;
            TaskBl = new WorkOrderTaskBl(this, _versionBl, _historyBl, _gevBl, _mapper, _workOrderTaskDal
                                , new WorkOrderTaskBlHelper(this, _versionBl, _historyBl, _gevBl, _mapper, _workOrderTaskDal, _workOrderStatusManager,_eventLogService));            
            _workOrderBlackOutBl = workOrderBlackOutBl;
            _userBl = userBl;
            _kafkaHelper = kafkaHelper;
            _serviceProvider = serviceProvider;
        }

        public WorkOrderBl(ApplicationDbContext dbContext, IWorkOrderVersionBl versionBl,
            IHistoryBl historyBl, IAuditLogBl auditBl,
            IWoGenericEnumValuesBl gevBl, IPostProcessingBl postProcessingBl,
            IMapper mapper,
            IWorkOrderBlValidations woValidations,
            IWorkOrderConfig workOrderConfig,
            IContractImportedFieldBl contractImportedFieldBl,
            IWorkOrderStatusRules workOrderStatusRule,
            IWorkOrderStatusManager workOrderStatusManager,
            IGeometryHelper geometryHelper,
            IWorkOrderDal workOrderDal,
            IServiceProvider serviceProvider) : base(dbContext)
        {
            _dbContext = dbContext;
            _gevBl = gevBl;
            _postProcessingBl = postProcessingBl;
            _mapper = mapper;
            _auditBl = auditBl;
            _historyBl = historyBl;
            _versionBl = versionBl;
            _woValidations = woValidations;
            _workOrderDal = workOrderDal;
            _geometryHelper = geometryHelper;
            _workOrderConfig = workOrderConfig;
            _woStatusRules = workOrderStatusRule;
            _workOrderStatusManager = workOrderStatusManager;
            _contractImportedFieldBl = contractImportedFieldBl;            
            includeExpressions = new List<Expression<Func<WorkOrderModel, object>>> {
                x=>x.StatusWeb
            };
            _serviceProvider = serviceProvider;
        }

        public WorkOrderBl(ApplicationDbContext dbContext, IMapper mapper,
    IWorkOrderDal workOrderDal) : base(dbContext)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _workOrderDal = workOrderDal;
            includeExpressions = new List<Expression<Func<WorkOrderModel, object>>> {
                x=>x.StatusWeb
            };
        }

        public WorkOrderModel FindByName(string name) => base.FindByName(name);

        public WorkOrderModel GetFirst(Expression<Func<WorkOrderModel, bool>> expressionWhere)
        {
            includeExpressions.Add(x => x.PostProcessStatus);
            WorkOrderModel workOrder = _dal.GetAllIQueryable(expressionWhere, includeExpressions).SingleOrDefault();
            if (workOrder.IsNull())
                return null;
            ContractModel contract = _contractBl.GetByName(workOrder.ContractName);
            workOrder.ContractTitle = contract?.ContractName;
            workOrder.ContractTimeZone = contract?.TimeZone;
            SetWorkOrderNameInVersion(workOrder.Versions);
            return workOrder;
        }

        public WorkOrderModel GetWithTasks(Expression<Func<WorkOrderModel, bool>> expressionWhere)
        {
            includeExpressions.Add(x => x.Tasks.OrderBy(z => z.Id));
            return _dal.GetAllIQueryable(expressionWhere, includeExpressions).SingleOrDefault();
        }

        public WorkOrderModel Get(Expression<Func<WorkOrderModel, bool>> expressionWhere)
        {
            WorkOrderModel workOrder = _dal.GetOne(expressionWhere, includeExpression: includeExpressions.ToArray());
            SetWorkOrderNameInVersion(workOrder.Versions);
            return workOrder;
        }

        public WorkOrderModel GetByName(string woName, bool includeTasks = false, bool noTracking = false)
        {
            var query = _dal.GetAllIQueryable(x => x.Name == woName, includeExpressions: includeExpressions.ToList(), noTracking: noTracking);

            if (includeTasks) query = query.Include(x => x.Tasks).ThenInclude(t => t.TaskStatus);

            WorkOrderModel workOrder = query.FirstOrDefault();
            return workOrder;
        }

        public IQueryable<WorkOrderModel> GetListWithoutVersion(Expression<Func<WorkOrderModel, bool>> expressionWhere = null)
        {
            return _dal.GetAllIQueryable(expressionWhere, new List<Expression<Func<WorkOrderModel, object>>> { x => x.StatusWeb }, noTracking: true);
        }

        public List<WorkOrderModel> GetAll(string contract, DateTime startD, DateTime endD,
            List<string> woStatusNames, OlameterFramework.OUIFramework.RequestDto filter, InstallDateOption installDateOption = InstallDateOption.Custom)
        {
            var workOrders = _dal.GetAll(contract, startD, endD, woStatusNames, filter, installDateOption);
            return workOrders;
        }

        public IAsyncEnumerable<WorkOrderModel> GetAllAsync(string contract, DateTime startD, DateTime endD, List<string> woStatusNames, OlameterFramework.OUIFramework.RequestDto filter, InstallDateOption installDateOption = InstallDateOption.Custom)
        {
            return _dal.GetAllAsync(contract, startD, endD, woStatusNames, filter, installDateOption).ToAsyncEnumerable();
        }

        public IQueryable<FsrCountDto> GetActiveFsrCount(string contract, IEnumerable<string> fsrList)
        {
            return _dal.GetActiveFsrCount(contract, fsrList);
        }

        public IQueryable<FsrCountDto> GetFsrCount(string contract, IEnumerable<string> fsrList)
        {
            return _dal.GetFsrCount(contract, fsrList);
        }

        public IQueryable<TeamCountDto> GetActiveTeamCount(string contract, IEnumerable<int> teamList)
        {
            return _dal.GetActiveTeamCount(contract, teamList);
        }

        public IQueryable<TeamCountDto> GetTeamCount(string contract, IEnumerable<int> teamList)
        {
            return _dal.GetTeamCount(contract, teamList);
        }

        public WorkOrderModel GetByName(string contractName, string workOrderName, bool noTracking = false)
        {
            return _dal.GetByName(contractName, workOrderName, noTracking);
        }

        private IQueryable<WorkOrderHelpdeskModel> GetByContractAndPhoneBase(TwilioSearchDto search)
        {
            IEnumerable<string> contractNames = new List<string>();

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = null;
            using (DaprClient daprClient = new DaprClientBuilder().UseJsonSerializationOptions(options).Build())
            {
                HttpRequestMessage clientRequest = daprClient.
                    CreateInvokeMethodRequest("wfms-groupmanagement-api"
                        , "TwilioAccount/GetContractNameByTwilioAccount", search.ContractName);

                contractNames = new ConcurrentBag<string>(daprClient.InvokeMethodAsync<IEnumerable<string>>(clientRequest).Result);
            }

            IQueryable<ContractImportedFieldModel> contractImportedFields = _contractImportedFieldBl.GetAll_IQueryable(contractImportedField =>
                                                                            contractNames.Any(contractname => contractImportedField.ContractName == contractname) && !contractImportedField.IsDeleted);

            IQueryable<WorkOrderModel> workOrders = GetList(workorder => contractNames.Any(contractname => contractname == workorder.ContractName));

            IQueryable<ContractModel> contracts = _contractBl.GetAll_IQueryable().Where(contract =>
                contractNames.Any(contractName => contractName == contract.Name));

            ApplyHelpdeskFilter(search, contractImportedFields, ref workOrders);

            return workOrders
                .Select(workOrder => new WorkOrderHelpdeskModel
                {
                    ServiceType = workOrder.ServiceType,
                    Address = workOrder.JsonData.RootElement.GetProperty("Contact").GetProperty("Address").GetString(),
                    Email = workOrder.JsonData.RootElement.GetProperty("Contact").GetProperty("Email").GetString(),
                    CustomerName = workOrder.JsonData.RootElement.GetProperty("Contact").GetProperty("CustomerName").GetString(),
                    Cycle = workOrder.JsonData.RootElement.GetProperty("ServicePoint").GetProperty("Cycle").GetString(),
                    WorkOrderId = workOrder.JsonData.RootElement.GetProperty("WorkOrder").GetProperty("WorkOrderId").GetString(),
                    AccountNumber = workOrder.JsonData.RootElement.GetProperty("Contact").GetProperty("CustomerAccountNumber").GetString(),
                    AMIMeterNumber = workOrder.JsonData.RootElement.GetProperty("Asset").GetProperty("ProposedAMIMeterNumber").GetString(),
                    LegacyMeterNumber = workOrder.JsonData.RootElement.GetProperty("Asset").GetProperty("LegacySerialNumber").GetString(),
                    Status = workOrder.StatusWebString,
                    WorkOrderName = workOrder.Name,
                    PlannedInstallDate = workOrder.InstallDate,
                    ContractTitle = contracts.Single(x => x.Name == workOrder.ContractName).ContractName,
                    PhoneNumber = workOrder.JsonData.RootElement.GetProperty("Contact").GetProperty("CellPhoneNo").GetString(),


                });
        }

        public IEnumerable<WorkOrderHelpdeskModel> GetByContractAndPhone(TwilioSearchDto search)
        {

            Stopwatch watch = new Stopwatch();
            watch.Start();
            IEnumerable<WorkOrderHelpdeskModel> result = GetByContractAndPhoneBase(search)
                .ToList(); ;
            watch.Stop();

            LoggerUtil.LogDebug($"Duration:{watch.Elapsed.TotalSeconds}");
            return result;
        }

        public IEnumerable<UpdatedWorkOrderDto> GetUpdatedWorkOrders(IEnumerable<string> workOrders)
        {
            var wos = _workOrderDal.GetAllIQueryable(x => workOrders.Any(wo => wo == x.Name), null, true).Include(x => x.TeamDispatch).ThenInclude(x => x.TeamDispatchTechs);
            IEnumerable<UpdatedWorkOrderDto> result = _mapper.Map<List<UpdatedWorkOrderDto>>(wos);

            return result;
        }

        public async IAsyncEnumerator<WorkOrderHelpdeskModel> GetByContractAndPhoneAsync(TwilioSearchDto search)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            IAsyncEnumerator<WorkOrderHelpdeskModel> result = GetByContractAndPhoneBase(search)
                .AsAsyncEnumerable()
                .GetAsyncEnumerator();
            watch.Stop();

            LoggerUtil.LogDebug($"Duration:{watch.Elapsed.TotalSeconds}");
            while (await result.MoveNextAsync())
            {
                yield return result.Current;
            }
        }

        private void ApplyHelpdeskFilter(TwilioSearchDto search, IQueryable<ContractImportedFieldModel> contractImportedFields, ref IQueryable<WorkOrderModel> workOrders)
        {
            foreach (var property in search.GetType().GetProperties().Where(prop => prop.Name != "ServiceType" && prop.Name != "ContractName" && prop.Name != "PlannedInstallDate"))
            {
                if (property.GetValue(search, null) != null && !(bool)property.GetValue(search, null)?.ToString().IsNullOrEmpty())
                {
                    IQueryable<HelpDeskFieldDto> fields = contractImportedFields
                                                 .Where(field => EF.Functions.ILike(field.Name, $"%{property.Name}%"))
                                                 .Select(workOrder => new HelpDeskFieldDto { Category = workOrder.Category, Name = workOrder.Name }).Distinct();
                    string jsonValue = $"%{property.GetValue(search, null)}%";

                    workOrders = workOrders.Where(workorder => fields.Any(field => EF.Functions.ILike((string)(object)workorder.JsonData.RootElement.GetProperty(field.Category).GetProperty(field.Name), jsonValue)));
                }
            }

            if (search.PlannedInstallDate.HasValue)
            {
                workOrders = workOrders.Where(workorder => workorder.InstallDate == search.PlannedInstallDate);
            }
        }

        public IAsyncEnumerable<WorkOrderModel> GetListAsync(Expression<Func<WorkOrderModel, bool>> expressionWhere)
        {
            return _dal.GetAllIQueryable(expressionWhere, noTracking: true)
                               .ToAsyncEnumerable();
        }

        public IQueryable<WorkOrderModel> GetList(Expression<Func<WorkOrderModel, bool>> expressionWhere = null, bool noTracking = true)
        {
            return _dal.GetAllIQueryable(expressionWhere, noTracking: noTracking);
        }

        public List<WorkOrderModel> SaveFromManuelSync(List<WorkOrderModel> workOrders, string username, bool andSave = false,string token=null)
        {
            using (IDbContextTransaction transaction = _dal.DbContext.Database.BeginTransaction())
            {
                try
                {
                    includeExpressions.Add(x => x.PostProcessStatus);
                    var workOrderNames = workOrders.Select(x => x.Name).ToList();
                    var existings = _dal.GetAllIQueryable(x => workOrderNames.Contains(x.Name), includeExpressions: includeExpressions).Include(x => x.StatusWeb).ToList();
                    workOrders.ForEach(model =>
                    {
                        var existing = existings.Where(x => x.Name == model.Name).FirstOrDefault();
                        UpdateFromManuelSync(model, existing, username, false,token);
                    });

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }

            if (andSave) _dal.SaveChanges();

            return workOrders;
        }

        /// <summary>
        /// Delete function only use in integration Test to clean.
        /// </summary>
        /// <param name="workOrders"></param>
        /// <param name="andSave"></param>
        /// <returns></returns>
        public List<WorkOrderModel> DeleteList(List<WorkOrderModel> workOrders, bool andSave = false)
        {
            workOrders.ForEach(model =>
            {
                IEnumerable<FileLocationModel> files = _dal.DbContext.Set<FileLocationModel>().Where(file => file.WorkOrder.Name == model.Name).ToList();
                _dal.DbContext.RemoveRange(files);
                Delete(model);
            });

            if (andSave) _dal.SaveChanges();

            return workOrders;
        }

        public List<WorkOrderModel> CreateList(List<WorkOrderModel> workOrders, string username, bool andSave = false)
        {
            workOrders.ForEach(model =>
            {
                Create(model, username, true);
            });

            if (andSave)
                _dal.SaveChanges();

            return workOrders;
        }

        public bool Exist(WorkOrderModel workOrder)
        {
            ContractImportedFieldModel workOrderIdField = _contractImportedFieldBl.GetAll_IQueryable(x => x.ContractName == workOrder.ContractName && x.Name == WorkOrderFieldName.WorkOrderIdFN && !x.IsDeleted).First();
            string workOrderId = workOrder?.JsonData.Get($"{workOrderIdField.Category}.{workOrderIdField.Name}").ToString();
            
            if(workOrder.ContractName == "Contract_84733114-bc5b-47e8-935b-6bf8cdaf54f4" || workOrder.ContractName == "Contract_4f4200ca-4fdb-4028-ac7a-8ada5896df5b")
                LoggerUtil.LogWarning(new Exception("Log WorkOrder Import NBP/KLINE"),$"WorkOrder Id:{workOrderId} Field:{workOrderIdField.ToJsonString()} workOrder:{workOrder.ToJsonString()}");
            
            return _workOrderDal.Exist(workOrder.ContractName, workOrderId);
        }

        public long GetId(WorkOrderModel workOrder)
        {
            ContractImportedFieldModel workOrderIdField = _contractImportedFieldBl.GetAll_IQueryable(x => x.ContractName == workOrder.ContractName && x.Name == WorkOrderFieldName.WorkOrderIdFN && !x.IsDeleted).First();
            string workOrderId = workOrder?.JsonData.Get($"{workOrderIdField.Category}.{workOrderIdField.Name}").ToString();
            return _workOrderDal.GetId(workOrder.ContractName, workOrderId, workOrderIdField);
        }

        public WorkOrderModel Create(WorkOrderModel workOrder, string username, bool andSave = false)
        {
            WorkOrderModel result = null;

            using (IDbContextTransaction transaction = _dbContext.Database.BeginTransaction())
            {
                try
                {
                    Dictionary<string, ContractImportedFieldModel> contractfields = _contractImportedFieldBl.GetAll_IQueryable(x => x.ContractName == workOrder.ContractName
                                                                                       && (x.Name == WorkOrderFieldName.WorkOrderIdFN
                                                                                            || x.Name == WorkOrderFieldName.InstallDateFN
                                                                                            || x.Name == WorkOrderFieldName.LatitudeFN
                                                                                            || x.Name == WorkOrderFieldName.LongitudeFN
                                                                                            || x.Name.ToLower() == WorkOrderFieldName.RecordTypeFN.ToLower()
                                                                                            || x.Name.ToLower() == WorkOrderFieldName.InstallFlagFN.ToLower()
                                                                                            || x.Name.ToLower() == WorkOrderFieldName.AMInclusionExclusionFN.ToLower())
                                                                                       && !x.IsDeleted).ToDictionary(x => x.Name, x => x);
                    string workOrderIdPath = $"{contractfields[WorkOrderFieldName.WorkOrderIdFN].Category}.{contractfields[WorkOrderFieldName.WorkOrderIdFN].Name}";


                    if (workOrder.StatusWeb == null)
                    {
                        //Default status if not specified
                        _workOrderStatusManager.SetWorkOrderStatus(workOrder, _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), StatusWeb.ReadyForDispatch.ToStringValue()));
                    }
                    else if (workOrder.StatusWeb.Id == 0) //For the integration Test
                    {

                        _workOrderStatusManager.SetWorkOrderStatus(workOrder, _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), workOrder.StatusWebString));
                    }

                    workOrder.PostProcessStatus = _gevBl.GetOrCreate(PostProcessStatus.PostProcessingWoStillOpen.GetEnumName(), PostProcessStatus.PostProcessingWoStillOpen.ToStringValue());
                    _woValidations.Validations(workOrder, workOrderIdPath);

                    WorkOrderVersion version = _mapper.Map<WorkOrderVersion>(workOrder);
                    version.CreatedOn = DateTime.UtcNow;

                    bool existing = Exist(workOrder);

                    if (existing)
                        throw new InvalidOperationException($"WorkOrder already exists. WorkOrderId = {workOrder?.JsonData.Get(workOrderIdPath).ToString()} ContractName={workOrder.ContractName}");

                    version.Version = 1;

                    string latitude = contractfields.ContainsKey(WorkOrderFieldName.LatitudeFN) ? workOrder?.JsonData.Get($"{contractfields[WorkOrderFieldName.LatitudeFN].Category}.{contractfields[WorkOrderFieldName.LatitudeFN].Name}")?.ToString() : string.Empty;
                    string longitude = contractfields.ContainsKey(WorkOrderFieldName.LatitudeFN) ? workOrder?.JsonData.Get($"{contractfields[WorkOrderFieldName.LongitudeFN].Category}.{contractfields[WorkOrderFieldName.LongitudeFN].Name}")?.ToString() : string.Empty;

                    var geo = _geometryHelper.GetDefaultPosition(latitude, longitude, _geofenceRadius);
                    workOrder.Position = geo.Position;

                    SetInstallDate(workOrder, contractfields);

                    if (workOrder.Position != null)
                        workOrder.Geofence = geo.Geofence;
                    version.IsSelect = true;
                    workOrder.Versions.Add(version);
                    result = _workOrderDal.Create(workOrder, andSave);

                    WorkOrderModel newModel = _mapper.Map<WorkOrderModel>(result);
                    newModel.Id = result.Id;
                    _historyBl.Create(newModel, HistoryType.WOCreated, username, andSave);

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return result;
        }

        public WorkOrderModel Update(WorkOrderModel workOrder, string username, bool andSave = false, bool inTrans = true, bool isAdmin = false)
        {
            if (inTrans) return UpdateTrans(workOrder, username, andSave, isAdmin);

            return this.InnerUpdate(workOrder, username, andSave, null, isAdmin);
        }

        public WorkOrderModel UpdateFromEvent(WorkOrderModel workOrder, string username, bool andSave = false, DateTimeOffset? eventDate = null)
        {
            var existing = _workOrderDal.GetByName(workOrder.Name, withStatusWeb: true, withVersions: true, withPostProcessStatus: true);

            if (existing.IsNull()) throw new KeyNotFoundException("Work Order to update not found"); // It is an update, the WorkOrder must existe

            if (!string.IsNullOrEmpty(workOrder.NewStatusWebString))
            {
                //New status
                var statusWeb = _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), workOrder.NewStatusWebString, andSave: andSave);

                if (_workOrderStatusManager.CanChangeStatus(existing.StatusWeb, statusWeb))
                {
                    workOrder.StatusWeb = statusWeb;
                    workOrder.StatusWebId = statusWeb.Id;
                }
                else
                {
                    workOrder.StatusWeb = existing.StatusWeb;
                    workOrder.StatusWebId = existing.StatusWebId;
                }                
            }

            if (workOrder.PostProcessStatus != null || !string.IsNullOrEmpty(workOrder.PostProcessStatusString))
            {
                workOrder.PostProcessStatus = workOrder.PostProcessStatus != null
                 ? _gevBl.GetOrCreate(workOrder.PostProcessStatus.TypeOrGroup, workOrder.PostProcessStatus.Name, andSave: andSave)
                 : _gevBl.GetOrCreate(PostProcessStatus.PostProcessingWoStillOpen.GetEnumName(), workOrder.PostProcessStatusString, andSave: andSave);
            }
            else
            {
                workOrder.PostProcessStatus = existing.PostProcessStatus;
            }

            WorkOrderModel updatedModel = BaseUpdate(workOrder, existing, username, andSave, eventDate);

            return updatedModel;
        }

        private WorkOrderModel UpdateTrans(WorkOrderModel workOrder, string username, bool andSave = false, bool isAdmin = false)
        {
            WorkOrderModel updatedModel = null;
            using (IDbContextTransaction transaction = _dbContext.Database.BeginTransaction())
            {
                try
                {
                    updatedModel = this.InnerUpdate(workOrder, username, andSave, null, isAdmin);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return updatedModel;
        }

        public WorkOrderModel InnerUpdate(WorkOrderModel workOrder, string username, bool andSave = false, WorkOrderModel existing = null, bool isAdmin = false)
        {
            existing = existing ?? _workOrderDal.GetByName(workOrder.Name, withStatusWeb: true, withVersions: true, withPostProcessStatus: true);

            if (existing.IsNull()) throw new KeyNotFoundException("Work Order to update not found"); // It is an update, the WorkOrder must existe

            if (!string.IsNullOrEmpty(workOrder.NewStatusWebString))
            {
                //Set new status
                var statusWeb = _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), workOrder.NewStatusWebString, andSave: andSave);
                _workOrderStatusManager.SetWorkOrderStatus(workOrder, statusWeb, isAdmin);
            }
            else if (workOrder.StatusWeb.IsNotNull() || !string.IsNullOrEmpty(workOrder.StatusWebString))
            {
                //Set new status
                var statusWeb = string.IsNullOrEmpty(workOrder.StatusWebString)
                ? _gevBl.GetOrCreate(workOrder.StatusWeb.TypeOrGroup, workOrder.StatusWeb.Name, andSave: andSave)
                : _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), workOrder.StatusWebString, andSave: andSave);

                _workOrderStatusManager.SetWorkOrderStatus(workOrder, statusWeb, isAdmin);
            }
            else
            {
                //Use current status
                _workOrderStatusManager.SetWorkOrderStatus(workOrder, existing.StatusWeb, isAdmin);
            }

            if (workOrder.PostProcessStatus != null || !string.IsNullOrEmpty(workOrder.PostProcessStatusString))
            {
                workOrder.PostProcessStatus = workOrder.PostProcessStatus != null
                 ? _gevBl.GetOrCreate(workOrder.PostProcessStatus.TypeOrGroup, workOrder.PostProcessStatus.Name, andSave: andSave)
                 : _gevBl.GetOrCreate(PostProcessStatus.PostProcessingWoStillOpen.GetEnumName(), workOrder.PostProcessStatusString, andSave: andSave);
            }
            else
            {
                workOrder.PostProcessStatus = existing.PostProcessStatus;
            }

            WorkOrderModel updatedModel = BaseUpdate(workOrder, existing, username, andSave, null, isAdmin);

            return updatedModel;
        }

        public List<WorkOrderModel> RetryFailedPostProcess(string username)
        {
            var postProcessStatus = _gevBl.GetOrCreate(PostProcessStatus.PostProcessingEtvFail.GetEnumName(), PostProcessStatus.PostProcessingEtvFail.ToStringValue(), andSave: true);
            var workOrders = _dal.FindBy_IQueryable(x => x.PostProcessStatusId == postProcessStatus.Id, includeExpression: x => x.Tasks).ToList();

            workOrders.ForEach(model =>
            {
                _postProcessingBl.RetryPostProcess(model, username);
            });

            return workOrders;
        }

        /// <summary>
        /// Method to update only the StatusWeb, AuditMode and the JsonData
        /// </summary>
        /// <param name="workOrder"></param>
        /// <param name="username"></param>
        /// <param name="andSave"></param>
        /// <returns></returns>
        public WorkOrderModel UpdateFromManuelSync(WorkOrderModel workOrder, WorkOrderModel existing, string username, bool andSave = false,string token = null)
        {
            WorkOrderModel updatedModel = null;
            var downloadStatus = _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), StatusWeb.Downloaded.ToStringValue(), andSave: true);
            var recallStatus = _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), StatusWeb.RecallRequested.ToStringValue(), andSave: true);

            if (existing != null)
            {
                //Sync is only allowed when wo is in downloaded or recall status
                if (existing.StatusWeb.Id == downloadStatus.Id || existing.StatusWeb.Id == recallStatus.Id)
                {
                    var statusWeb = workOrder.StatusWeb != null
                         ? _gevBl.GetOrCreate(workOrder.StatusWeb.TypeOrGroup, workOrder.StatusWeb.Name, andSave: andSave)
                         : _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), workOrder.StatusWebString, andSave: andSave);

                    _workOrderStatusManager.SetWorkOrderStatus(existing, statusWeb);

                    //WARNING!!! This code override data with one from the mobile. This logic need to be revised 
                    //It mean if wo is downloaded then web app change data for the wo after a full sync those change
                    //will be gone and replaced with value from the mobile.
                    existing.AuditMode = workOrder.AuditMode;
                    existing.JsonData = workOrder.JsonData;
                    //Waiting for the change in mobile
                    //existing.JsonData = ManualMappers.ExternalJsonUpdate(workOrder.JsonData, existing.JsonData);

                    updatedModel = BaseUpdate(existing, existing, username, andSave);
                }
                else
                {
                    updatedModel = existing;
                    LoggerUtil.LogWarning($"WorkOrder {existing.Name} is not allowed to be sync by {username} because it's status is {existing.StatusWeb.Name}");
                }

                updatedModel = _postProcessingBl.RunPostProcess(new List<WorkOrderModel> { updatedModel }, username, token).FirstOrDefault();
            }

            return updatedModel;
        }

        private void SetInstallDate(WorkOrderModel workOrder, Dictionary<string, ContractImportedFieldModel> contractImportedFields)
        {
            if (contractImportedFields.ContainsKey(WorkOrderFieldName.InstallDateFN)
                && (bool)workOrder?.JsonData?.Exist($"{contractImportedFields[WorkOrderFieldName.InstallDateFN].Category}.{contractImportedFields[WorkOrderFieldName.InstallDateFN].Name}"))
            {
                string installDatePath = $"{contractImportedFields[WorkOrderFieldName.InstallDateFN].Category}.{contractImportedFields[WorkOrderFieldName.InstallDateFN].Name}";
                workOrder.InstallDate = new DateTime(Convert.ToDateTime(workOrder?.JsonData.Get(installDatePath)).Ticks, DateTimeKind.Utc).Date;

                workOrder.JsonData = workOrder.JsonData?.DeleteKeyValues(new List<string> { installDatePath });
            }
        }

        private WorkOrderModel BaseUpdate(WorkOrderModel workOrder, WorkOrderModel existing, string username, bool andSave = false, DateTimeOffset? eventDate = null, bool isAdmin = false)
        {
            try
            {
                Dictionary<string, ContractImportedFieldModel> contractImportedFields = _contractImportedFieldBl.GetAll_IQueryable(x => x.ContractName == workOrder.ContractName
                                                                            && (x.Name == WorkOrderFieldName.WorkOrderIdFN || x.Name == WorkOrderFieldName.InstallDateFN)
                                                                            && !x.IsDeleted)
                .ToDictionary(x => x.Name, x => x);

                if (!contractImportedFields.ContainsKey(WorkOrderFieldName.WorkOrderIdFN))
                    throw new Exception($"WorkOrder {workOrder.Name} is missing the WorkOrderId");

                string workOrderIdPath = $"{contractImportedFields[WorkOrderFieldName.WorkOrderIdFN].Category}.{contractImportedFields[WorkOrderFieldName.WorkOrderIdFN].Name}";

                _woValidations.Validations(workOrder, workOrderIdPath);
                WorkOrderVersion version = _versionBl.CreateAndAdd(workOrder, existing);
                _woValidations.Validation(workOrder, existing, workOrderIdPath);
                workOrder.Id = existing.Id;

                //What is the logic/usecase of this condition?
                if ((existing.Audited && workOrder.Audited) || (!existing.Audited && workOrder.Audited) || (!existing.Audited && !workOrder.Audited))
                {
                    workOrder.CreatedOn = existing.CreatedOn;
                    DateTime? existingInstallDate = existing.InstallDate;
                    _workOrderStatusManager.SetWorkOrderStatus(existing, workOrder.StatusWeb, isAdmin, eventDate);
                    existing = _mapper.Map(workOrder, existing);
                    existing.InstallDate = existingInstallDate;                    

                    if (workOrder.PostProcessStatus.IsNotNull()) existing.PostProcessStatusId = workOrder.PostProcessStatus.Id;
                }

                SetInstallDate(existing, contractImportedFields);

                //Convert to utc
                existing.StartDateBlackout = workOrder.StartDateBlackout.HasValue ? workOrder.StartDateBlackout.Value.ToUniversalTime().Date : null;
                existing.EndDateBlackout = workOrder.EndDateBlackout.HasValue ? workOrder.EndDateBlackout.Value.ToUniversalTime().Date : null;

                ApplyWorkOrderStatusInBlackout(existing);
                SetWoAssignByStatus(existing, version);
                _historyBl.Create(existing, HistoryType.WOUpdated, username, andSave);

                WorkOrderModel updatedModel = _workOrderDal.Update(existing, andSave);
                _versionBl.SetAsValid(version, andSave);

                return updatedModel;
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError($"{workOrder.Name}");
                throw;
            }
        }

        //Internal and virtual for unittests
        internal virtual void ApplyWorkOrderStatusInBlackout(WorkOrderModel workOrder)
        {
            WoGenericEnumValuesBl internWoGenericEnumValuesBl = null;
            GenericEnumValue status = null;

            try
            {
                //InBlackout
                if (workOrder.IsInBlackOut &&
                    _woStatusRules.BlackoutWhiteList(workOrder.StatusWeb.Name))
                {
                    internWoGenericEnumValuesBl = new WoGenericEnumValuesBl(_workOrderDal.DbContext);                   

                    if (workOrder.StatusWeb.Name == StatusWeb.Downloaded.ToStringValue())
                    {
                        status = internWoGenericEnumValuesBl.GetOrCreate(StatusWeb.RecallRequested.GetEnumName(), StatusWeb.RecallRequested, null, true);
                    }
                    else
                    {
                        status = internWoGenericEnumValuesBl.GetOrCreate(StatusWeb.OnHold.GetEnumName(), StatusWeb.OnHold, null, true);
                    }

                    _workOrderStatusManager.SetWorkOrderStatus(workOrder, status);
                }
            }
            catch (Exception ex)
            {
                var woMsg = "WorkOrder = ";

                if(workOrder == null)
                {
                    woMsg += "NULL\n";
                }
                else
                {
                    woMsg += $"IsInBlackOut: {workOrder.IsInBlackOut} StatusWeb: ";

                    if (workOrder.StatusWeb == null) 
                    {
                        woMsg += $"NULL\n";
                    }
                    else
                    {
                        woMsg += $"{workOrder.StatusWeb.Name}\n";
                    }

                }

                var woStatusRulesMsg = "";
                if (_woStatusRules == null) woStatusRulesMsg = "woStatusRules is NULL\n";

                var workOrderDalMsg = "";
                if (_workOrderDal == null) workOrderDalMsg = "workOrderDal is NULL\n";

                var internWoGenericEnumValuesBlMsg = "";
                if (internWoGenericEnumValuesBl == null) internWoGenericEnumValuesBlMsg = "internWoGenericEnumValuesBl is NULL\n";

                var statusMsg = "";
                if (status == null) statusMsg = "status is NULL\n";

                var workOrderStatusManagerMsg = "";
                if (_workOrderStatusManager == null) workOrderStatusManagerMsg = "workOrderStatusManager is NULL\n";

                LoggerUtil.LogError($"Error in ApplyWorkOrderStatusInBlackout:\n{woMsg}{woStatusRulesMsg}{workOrderDalMsg}{internWoGenericEnumValuesBlMsg}{statusMsg}{workOrderStatusManagerMsg}");
                throw;
            }


        }

        public void QueryPosition(WorkOrderModel model)
        {
            IQueryable<ContractImportedFieldModel> contractImportFields = _dbContext.Set<ContractImportedFieldModel>()
                                                                                  .Where(x => x.ContractName == model.ContractName
                                                                                              && (x.Name == WorkOrderFieldName.WorkOrderIdFN
                                                                                                  || x.Name == WorkOrderFieldName.AddressFN
                                                                                                  || x.Name == WorkOrderFieldName.LatitudeFN
                                                                                                  || x.Name == WorkOrderFieldName.LongitudeFN)
                                                                                              && !x.IsDeleted);

            string workOrderIdPath = contractImportFields.Where(x => x.Name == WorkOrderFieldName.WorkOrderIdFN).Select(x => $"{x.Category}.{x.Name}").Single();
            string address = contractImportFields.Where(x => x.Name == WorkOrderFieldName.AddressFN).Select(x => $"{x.Category}.{x.Name}").Single();
            string latitudeField = contractImportFields.Where(x => x.Name == WorkOrderFieldName.LatitudeFN).Select(x => $"{x.Category}.{x.Name}").Single();
            string longitudeField = contractImportFields.Where(x => x.Name == WorkOrderFieldName.LongitudeFN).Select(x => $"{x.Category}.{x.Name}").Single();

            double latitude = Convert.ToDouble(model.JsonData.Get(latitudeField)?.ToString());
            double longitude = Convert.ToDouble(model.JsonData.Get(longitudeField)?.ToString());
            Geometry position = new Point(longitude, latitude);

            if (string.IsNullOrEmpty(model.JsonData.Get(address)?.ToString()) && position.IsNotNull())
            {
                var r = new ReverseGeoCodeQueryEventModel
                {
                    Id = new Guid(),
                    Latitude = latitude,
                    Longitude = longitude,
                    Name = model.Name,
                    Source = Constants.WoedSource
                };
                _eventLogService.Publish<ReverseGeoCodeQueryIntegrationEvent>(EventAction.Create, r);
            }
            else if (position.Centroid.X == 0 && position.Centroid.Y == 0 && !string.IsNullOrEmpty(address))
            {
                address = model.JsonData.Get(address)?.ToString() ?? "";
                var g = new GeoCodeQueryEventModel
                {
                    Address = ReturnAddresModel(address),
                    Id = new Guid(),
                    Name = model.Name,
                    Source = Constants.WoedSource
                };
                _eventLogService.Publish<GeoCodeQueryIntegrationEvent>(EventAction.Create, g);
            }
        }
        /// <summary>
        /// Update the location (Address or Latitude, Longitude) of a workOrder based on the result provided by the Geocode service)
        /// </summary>
        /// <param name="name">Name of the workOrder</param>
        /// <param name="geocodeResult">Response received by the GeoCode Service</param>
        public void UpdatePosition(string name, GeoCodeResultEventModel geocodeResult)
        {
            WorkOrderModel currentWo = _dal.GetAllIQueryable(x => x.Name == name).SingleOrDefault();

            if (currentWo == null)
                return;

            IQueryable<ContractImportedFieldModel> contractImportFields = _dbContext.Set<ContractImportedFieldModel>()
                                                                                    .Where(x => x.ContractName == currentWo.ContractName
                                                                                                && (x.Name == WorkOrderFieldName.WorkOrderIdFN
                                                                                                    || x.Name == WorkOrderFieldName.AddressFN
                                                                                                    || x.Name == WorkOrderFieldName.LatitudeFN
                                                                                                    || x.Name == WorkOrderFieldName.LongitudeFN)
                                                                                                && !x.IsDeleted);
            string longitude = contractImportFields.Where(x => x.Name == WorkOrderFieldName.LongitudeFN).Select(x => $"{x.Category}.{x.Name}").Single();
            string latitude = contractImportFields.Where(x => x.Name == WorkOrderFieldName.LatitudeFN).Select(x => $"{x.Category}.{x.Name}").Single();
            string workOrderId = contractImportFields.Where(x => x.Name == WorkOrderFieldName.WorkOrderIdFN).Select(x => $"{x.Category}.{x.Name}").Single();
            string address = contractImportFields.Where(x => x.Name == WorkOrderFieldName.AddressFN).Select(x => $"{x.Category}.{x.Name}").Single();

            includeExpressions.Add(x => x.PostProcessStatus);

            currentWo.Position = GISUtilities.TransformProjection(_dal.DbContext, geocodeResult.Longitude, geocodeResult.Latitude, GISUtilities.Srid4326, geocodeResult.SRID);
            currentWo.Position.SRID = geocodeResult.SRID;
            currentWo.PositionSRID = geocodeResult.SRID;
            if (currentWo.Position != null)
                currentWo.Geofence = currentWo.Position.Buffer(_geofenceRadius);

            string currentAddress = currentWo.JsonData.Get(address) != null
                                    ? currentWo.JsonData.Get(address).ToString()
                                    : String.Empty;

            currentWo.JsonData = currentWo.JsonData.Write(address, currentAddress.IsNullOrEmpty() ? ReturnAddress(geocodeResult.Address) : currentAddress);
            currentWo.JsonData = currentWo.JsonData.Write(longitude, geocodeResult.Longitude);
            currentWo.JsonData = currentWo.JsonData.Write(latitude, geocodeResult.Latitude);
            currentWo.JsonData = currentWo.JsonData.Write($"{contractImportFields.Single(x => x.Name == WorkOrderFieldName.LatitudeFN).Category}.{WorkOrderModel.SRID}", GISUtilities.Srid4326);

            Update(currentWo, VariousConstants.ImporterUserName, true);
        }

        private AddressModel ReturnAddresModel(string address)
        {
            var result = new AddressModel
            {
                FullAddress = address
            };
            return result;
        }
        private string ReturnAddress(AddressModel addressModel)
        {
            return string.Format("{0} {1} {2}, {3} {4}, {5}", addressModel.CivicNumber, addressModel.Road, addressModel.City, addressModel.State, addressModel.Country, addressModel.PostCode);
        }

        public virtual void CreateFirstTask(string workOrderName, string username, WorkOrderConfigDto woConfig = null)
        {
            var wo = _dal.GetAllIQueryable(x => x.Name == workOrderName, noTracking: true).Include(x => x.Tasks).ThenInclude(x => x.TaskStatus).FirstOrDefault();

            if (wo != null && wo.Tasks.Count == 0)
            {
                try
                {
                    CreateTask(wo, null, username, null, woConfig);
                }
                catch (Exception ex)
                {
                    LoggerUtil.LogError(ex, ex.Message);
                }
            }
        }

        public WorkOrderTaskModel GetFirstTaskTemplate(long workOrderId)
        {
            var workOrder = _dal.GetAllIQueryable(x => x.Id == workOrderId, noTracking: true).FirstOrDefault();
            var woConfig = _workOrderConfig.GetWorkOrderConfig(workOrder);
            var config = _mapper.Map<ExecutorModel.ConfigModel>(woConfig);
            var wo = _mapper.Map<ExecutorModel.WorkOrderModel>(workOrder);
            var nextStep = _executorService.Run(wo, null, config);

            if (nextStep.TaskTemplate == null)
            {
                var msg = "Cannot create next task because the TaskTemplate was not found.";

                if (woConfig.Conditions.Count == 0)
                {
                    msg += " Possible reason: No condition was set for the task in CFG.";
                }

                throw new Exception(msg);
            }

            var taskStatus = _gevBl.GetOrCreate(TaskStatus.TaskNone.GetEnumName(), TaskStatus.TaskNone.ToStringValue(), andSave: true);

            var firstTask = new WorkOrderTaskModel()
            {
                WorkOrderWorkFlowName = woConfig.WorkOrderWorkflowName,
                WorkOrderWorkFlowTaskName = nextStep.TaskTemplate.Name,
                TaskStatusString = TaskStatus.TaskNone.ToStringValue(),
                ActivityDetailString = ActivityDetail.WOTaskCreated.ToString(),
                ActivityTypeString = ActivityType.ActivityTypeEventName.ToStringValue(),
                TaskStatus = taskStatus
            };

            return firstTask;
        }

        public virtual void ReCreateFirstTask(string workOrderName, string username)
        {
            var wo = _dal.GetAllIQueryable(x => x.Name == workOrderName, noTracking: true).Include(x => x.Tasks).ThenInclude(x => x.TaskStatus).FirstOrDefault();

            if (wo != null && wo.Tasks.Any())
            {
                var t = wo.Tasks.OrderBy(x => x.Id).Last();
                if (t.TaskStatus.Name == TaskStatus.TaskNone.ToString())
                    t.TaskStatus = _gevBl.GetOrCreate(TaskStatus.TaskNone.GetGenericTypeName(),
                        TaskStatus.TaskNone.ToStringValue());
            }

            try
            {
                CreateTask(wo, null, username);
            }
            catch (Exception ex)
            {
                LoggerUtil.LogError(ex, ex.Message);
            }
        }

        public virtual void CreateNextTask(string workOrderName, WorkOrderTaskModel currentTask, string username)
        {
            CreateNextTask(workOrderName, currentTask, username, null);
        }

        private void CreateNextTask(string workOrderName, WorkOrderTaskModel currentTask, string username, ExecutorModel.NextStepModel nextStep)
        {
            var workOrder = _dal.GetAllIQueryable(x => x.Name == workOrderName, noTracking: true).Include(x => x.Tasks).ThenInclude(x => x.TaskStatus).FirstOrDefault();
            var woConfig = _workOrderConfig.GetWorkOrderConfig(workOrder);
            var config = _mapper.Map<ExecutorModel.ConfigModel>(woConfig);
            var wo = _mapper.Map<ExecutorModel.WorkOrderModel>(workOrder);
            var t = _mapper.Map<ExecutorModel.TaskModel>(currentTask);
            var appointmentTasks = _workOrderConfig.GetAppointmentTasks(workOrder).Where(x => !x.HasTaskToCreate);
            var task = _dal.DbContext.Set<WorkOrderTaskModel>().FirstOrDefault(x => x.WorkOrderTaskName == currentTask.WorkOrderTaskName);

            if (task != null) 
            {
                task.TaskStatus = _gevBl.GetOrCreate(TaskStatus.TaskNone.GetEnumName(), currentTask.TaskStatusString, andSave: true);
            }            

            if (nextStep == null)
            {
                if (appointmentTasks.Any(x => x.TaskName == t.Name))
                {
                    //If standalone appointment task
                    nextStep = new ExecutorModel.NextStepModel()
                    {
                        WorkorderStatus = workOrder.StatusWebString
                    };
                }
                else
                {
                    nextStep = _executorService.Run(wo, t, config);
                }
            }

            if (nextStep != null)
            {
                var woWebStatusName = nextStep.WorkorderStatus;
                var isNumeric = int.TryParse(woWebStatusName, out _);

                if (isNumeric)
                {
                    woWebStatusName = _workOrderConfig.GetCollectionItemById(Convert.ToInt64(nextStep.WorkorderStatus))?.AttributesSchemas.FirstOrDefault(x => x.Key == "GUID")?.Value;
                }

                var updateWo = _dal.GetAllIQueryable(x => x.Name == workOrderName).FirstOrDefault();
                var statusWeb = _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), woWebStatusName, andSave: true);
                _workOrderStatusManager.SetWorkOrderStatus(updateWo, statusWeb);
                SetWoAssignByStatus(updateWo);

                if (task != null)
                {
                    task.Comment = nextStep.WorkorderTaskComment;
                }                
            }

            if (workOrder != null && task != null && (task.TaskStatusString == TaskStatus.TaskCompleted.ToStringValue() || task.TaskStatusString == TaskStatus.TaskSkipped.ToStringValue()))
            {
                CreateTask(workOrder, task, username, nextStep, woConfig);
            }

            _dal.SaveChanges();
        }

        public WorkOrderModel UpdateAndCreateNextTask(WorkOrderModel workOrder, WorkOrderTaskModel currentTask, string username, bool inTrans = true)
        {
            WorkOrderModel ret = null;

            if (inTrans)
            {
                using (IDbContextTransaction transaction = _dbContext.Database.BeginTransaction())
                {
                    try
                    {
                        ret = Update(workOrder, username, true, false);
                        CreateNextTask(workOrder.Name, currentTask, username);
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            else
            {
                try
                {
                    ret = Update(workOrder, username, true, false);
                    CreateNextTask(workOrder.Name, currentTask, username);
                }
                catch (Exception ex)
                {
                    throw;
                }
            }            

            return ret;
        }

        public WorkOrderModel OptOutWorkOrder(WorkOrderModel workOrder, OptOutDto optOut, string username)
        {
            if (workOrder == null)
            {
                throw new Exception($"WorkOrder not found");
            }

            return StatusUpdateWorkOrder(new List<WorkOrderModel> { workOrder }, optOut, username).FirstOrDefault();
        }

        public WorkOrderModel CSRHoldWorkOrder(WorkOrderModel workOrder, CSRHoldDto csrHold, string username)
        {
            if (workOrder == null)
            {
                throw new Exception($"WorkOrder not found");
            }

            if (_woStatusRules.CSRHoldWhiteList(workOrder.StatusWeb.Name))
            {
                return StatusUpdateWorkOrder(new List<WorkOrderModel> { workOrder }, csrHold, username).FirstOrDefault();
            }

            throw new Exception($"Not allowed to set WorkOrder in CSRHold when status is: {workOrder.StatusWeb.Name}");
        }

        public WorkOrderModel AdminWorkOrder(WorkOrderModel workOrder, WoAdminDto woAdmin, string username, string token = null)
        {
            if (workOrder == null)
            {
                throw new Exception($"WorkOrder not found");
            }

            var updatedWo = StatusUpdateWorkOrder(new List<WorkOrderModel> { workOrder }, woAdmin, username, true).FirstOrDefault();

            if(updatedWo != null)
            {
                updatedWo = _postProcessingBl.RunPostProcess(new List<WorkOrderModel> { updatedWo }, username, token).FirstOrDefault();
                _dbContext.SaveChanges();
            }            

            return updatedWo;
        }

        public List<WorkOrderModel> StatusUpdateWorkOrder(List<WorkOrderModel> workOrders, StatusUpdateDto statusUpdate, string username, bool isAdmin = false)
        {
            if (workOrders == null)
            {
                throw new ArgumentNullException(nameof(workOrders));
            }

            if (workOrders.Count == 0)
            {
                return new List<WorkOrderModel>();
            }

            if (workOrders.Any(workOrder => workOrder == null))
            {
                throw new Exception("WorkOrder not found");
            }

            var currentTasksByWorkOrderId = new Dictionary<long, WorkOrderTaskModel>();
            var workOrderIdsByName = new Dictionary<string, long>();

            var workOrderIds = workOrders
                .Where(workOrder => workOrder != null && workOrder.Id > 0)
                .Select(workOrder => workOrder.Id)
                .Distinct()
                .ToList();
            var missingIdNames = workOrders
                .Where(workOrder => workOrder != null && workOrder.Id <= 0 && !string.IsNullOrWhiteSpace(workOrder.Name))
                .Select(workOrder => workOrder.Name)
                .Distinct()
                .ToList();

            if (missingIdNames.Count > 0)
            {
                workOrderIdsByName = _dal.GetAllIQueryable(workOrder => missingIdNames.Contains(workOrder.Name), noTracking: true)
                    .Select(workOrder => new { workOrder.Name, workOrder.Id })
                    .ToDictionary(workOrder => workOrder.Name, workOrder => workOrder.Id);
                workOrderIds.AddRange(workOrderIdsByName.Values);
                workOrderIds = workOrderIds.Distinct().ToList();
            }

            if (workOrderIds.Count > 0)
            {
                var currentTaskIds = _dal.DbContext.Set<WorkOrderTaskModel>()
                    .Where(task => workOrderIds.Contains(task.WorkOrderId))
                    .GroupBy(task => task.WorkOrderId)
                    .Select(group => new { group.Key, TaskId = group.Max(task => task.Id) })
                    .ToList();

                if (currentTaskIds.Count > 0)
                {
                    var taskIds = currentTaskIds.Select(task => task.TaskId).ToList();
                    var currentTasks = _dal.DbContext.Set<WorkOrderTaskModel>()
                        .Where(task => taskIds.Contains(task.Id))
                        .Include(task => task.TaskStatus)
                        .AsNoTracking()
                        .ToList();

                    currentTasksByWorkOrderId = currentTasks.ToDictionary(task => task.WorkOrderId);
                }
            }

            var user = _userBl.FindBy_IQueryable(x => x.UserName == username).FirstOrDefault();
            var woWebStatusName = statusUpdate.WebStatus != null
                ? statusUpdate.WebStatus.Name
                : _workOrderConfig.GetCollectionItemById(Convert.ToInt64(statusUpdate.WorkOrderStatus))?.AttributesSchemas.FirstOrDefault(x => x.Key == "GUID")?.Value;
            var statusWeb = _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), woWebStatusName, andSave: true);
            var taskSkippedStatus = _gevBl.GetOrCreate(TaskStatus.TaskSkipped.GetEnumName(), TaskStatus.TaskSkipped.ToStringValue(), andSave: true);
            var taskCompletedStatus = _gevBl.GetOrCreate(TaskStatus.TaskCompleted.GetEnumName(), TaskStatus.TaskCompleted.ToStringValue(), andSave: true);

            var modifiedWorkOrders = new List<WorkOrderModel>(workOrders.Count);

            using (IDbContextTransaction transaction = _dbContext.Database.BeginTransaction())
            {
                try
                {
                    foreach (var workOrder in workOrders)
                    {
                        _workOrderStatusManager.SetWorkOrderStatus(workOrder, statusWeb, isAdmin);
                        SetWoAssignByStatus(workOrder);
                        var modifiedWorkOrder = Update(workOrder, username, true, false, isAdmin);
                        modifiedWorkOrders.Add(modifiedWorkOrder);

                        var workOrderId = workOrder.Id;
                        if (workOrderId <= 0 && !string.IsNullOrWhiteSpace(workOrder.Name))
                        {
                            workOrderIdsByName.TryGetValue(workOrder.Name, out workOrderId);
                        }

                        if (workOrderId > 0 && currentTasksByWorkOrderId.TryGetValue(workOrderId, out var currentTask))
                        {
                            var currentTaskStatus = currentTask.TaskStatus?.Name ?? currentTask.TaskStatusString;
                            if (currentTaskStatus == TaskStatus.TaskNone.ToStringValue())
                            {
                                currentTask = _dal.DbContext.Set<WorkOrderTaskModel>().SingleOrDefault(x => x.Id == currentTask.Id);
                                if (currentTask != null)
                                {
                                    currentTask.Comment = statusUpdate.SkipCode;
                                    currentTask.TaskStatus = taskSkippedStatus;
                                    currentTask.CompletedBy = user != null ? user.DisplayName : "WOMS-T000000";
                                }
                            }
                        }

                        if (statusUpdate.TaskName != null)
                        {
                            //Create task                    
                            var nextStep = new ExecutorModel.NextStepModel()
                            {
                                WorkorderStatus = statusWeb.Name,
                                TaskTemplate = new ExecutorModel.TaskModel()
                                {
                                    Name = statusUpdate.TaskName,
                                    Status = TaskStatus.TaskCompleted.ToStringValue()
                                }
                            };

                            CreateTask(workOrder, null, username, nextStep);
                            _dal.SaveChanges();

                            //Complete task
                            var nextTask = _dal.GetAllIQueryable(x => x.Name == workOrder.Name, noTracking: true).Include(x => x.Tasks).FirstOrDefault()?.Tasks.OrderByDescending(x => x.Id).FirstOrDefault();

                            if (nextTask != null)
                            {
                                nextTask = _dal.DbContext.Set<WorkOrderTaskModel>().SingleOrDefault(x => x.Id == nextTask.Id);
                                if (nextTask != null)
                                {
                                    nextTask.Comment = statusUpdate.Comment;
                                    nextTask.TaskStatus = taskCompletedStatus;
                                    nextTask.CompletedBy = user != null ? user.DisplayName : "WOMS-T000000";

                                    ExecutorModel.NextStepModel ns = new ExecutorModel.NextStepModel()
                                    {
                                        WorkorderStatus = workOrder.StatusWeb.Name,
                                        WorkorderTaskComment = statusUpdate.SkipCode
                                    };

                                    CreateNextTask(workOrder.Name, nextTask, username, ns);
                                }
                            }
                        }

                        _dal.SaveChanges();

                        //Create next task if it's specified
                        if (!string.IsNullOrWhiteSpace(statusUpdate.NextTaskToCreate))
                        {

                            var nextTaskStep = new ExecutorModel.NextStepModel()
                            {
                                WorkorderStatus = statusWeb.Name,
                                TaskTemplate = new ExecutorModel.TaskModel()
                                {
                                    Name = statusUpdate.NextTaskToCreate,
                                    Status = TaskStatus.TaskNone.ToStringValue()
                                }
                            };

                            CreateTask(modifiedWorkOrder, null, username, nextTaskStep);
                            _dal.SaveChanges();
                        }
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return modifiedWorkOrders;
        }

        public WorkOrderModel ReleaseCSRHoldWorkOrder(string workOrderName, string username)
        {
            WorkOrderModel wo = null;

            using (IDbContextTransaction transaction = _dbContext.Database.BeginTransaction())
            {
                try
                {
                    //Get wo
                    wo = _workOrderDal.GetAllIQueryable(x => x.Name == workOrderName).Include(x => x.Tasks).FirstOrDefault();

                    if (wo != null && _woStatusRules.CSRHoldReleaseWhiteList(wo.StatusWeb.Name))
                    {
                        //Get previous task
                        var previousTask = wo.Tasks.OrderBy(x => x.Id).TakeLast(2).First();

                        if (previousTask.WorkOrderWorkFlowTaskName != wo.Tasks.LastOrDefault()?.WorkOrderWorkFlowTaskName)
                        {
                            //Create task                    
                            var newTask = _mapper.Map<WorkOrderTaskModel>(previousTask);
                            newTask.Id = 0;
                            newTask.Comment = "Remove workorder from customer hold";
                            newTask.CompletedBy = null;
                            newTask.TaskStatus = _gevBl.GetOrCreate(TaskStatus.TaskNone.GetEnumName(), TaskStatus.TaskNone.ToStringValue(), andSave: true);
                            newTask.Name = newTask.GetResourceIdentifierName();
                            newTask.WorkOrderTaskName = $"WOTask_{Guid.NewGuid()}_web";

                            wo.Tasks.Add(newTask);
                        }

                        //Set status based on previous status
                        var statusWeb = _gevBl.GetOrCreate(StatusWeb.None.GetEnumName(), GetStatusForCSRHoldRelease(wo).ToStringValue());
                        _workOrderStatusManager.SetWorkOrderStatus(wo, statusWeb, true);
                        SetWoAssignByStatus(wo);
                        _historyBl.Create(wo, HistoryType.WOUpdated, username, false);
                        _dbContext.SaveChanges();

                        transaction.Commit();

                        _workOrderBlackOutBl.CalculateProcessBlackOutAsync(DateTime.UtcNow, wo);
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return wo;
        }

        private StatusWeb GetStatusForCSRHoldRelease(WorkOrderModel workOrder)
        {
            StatusWeb status = StatusWeb.ReadyForDispatch;

            if (workOrder.IsInBlackOut)
            {
                status = StatusWeb.OnHold;
            }
            else
            {
                var auditLog = _auditBl.GetIQueryable(x => x.History.WorkOrder.Name == workOrder.Name && x.FieldName == "StatusWeb" && x.NewValue == StatusWeb.CSRHold.ToStringValue()).FirstOrDefault();

                if (auditLog != null)
                {
                    if (auditLog.OldValue == StatusWeb.OnHold.ToStringValue())
                    {
                        status = StatusWeb.OnHold;
                    }
                    else if (auditLog.OldValue == StatusWeb.UTC.ToStringValue())
                    {
                        status = StatusWeb.UTC;
                    }
                }
            }

            return status;
        }

        public WorkOrderModel CreateWorkOrderAppointment(string workOrderName, string username)
        {
            WorkOrderModel wo = null;

            using (IDbContextTransaction transaction = _dbContext.Database.BeginTransaction())
            {
                try
                {
                    wo = CreateWorkOrderAppointment(workOrderName, username, transaction);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw;
                }
            }

            return wo;
        }

        private WorkOrderModel CreateWorkOrderAppointment(string workOrderName, string username, IDbContextTransaction transaction)
        {
            WorkOrderModel wo = null;

            //Get wo
            wo = _workOrderDal.GetAllIQueryable(x => x.Name == workOrderName).Include(x => x.Tasks).FirstOrDefault();

            //Get appointment tasks from config
            var appointmentTasks = _workOrderConfig.GetAppointmentTasks(wo);

            //If appointment task is already created then do nothing
            if (!AppointmentTaskAlreadyCreated(wo, appointmentTasks))
            {
                //Find appointment task without any "next task creation" from config
                var task = appointmentTasks.Where(x => !x.HasTaskToCreate).FirstOrDefault();

                if (task == null)
                {
                    throw new Exception("No appointment task without next task");
                }

                //Create appointment task                    
                var nextStep = new ExecutorModel.NextStepModel()
                {
                    WorkorderStatus = wo.StatusWebString,
                    TaskTemplate = new ExecutorModel.TaskModel()
                    {
                        Name = task.TaskName,
                        Status = TaskStatus.TaskNone.ToStringValue()
                    }
                };

                CreateTask(wo, null, username, nextStep);

                //Reorder appointment task before the current task
                if (wo.Tasks.Count > 1 && wo.Tasks[wo.Tasks.Count - 2].TaskStatusString == TaskStatus.TaskNone.ToStringValue())
                {
                    var currentTask = wo.Tasks[wo.Tasks.Count - 2];
                    wo.Tasks.RemoveAt(wo.Tasks.Count - 2);
                    _dbContext.SaveChanges();
                    currentTask = _mapper.Map<WorkOrderTaskModel>(currentTask);
                    wo.Tasks.Add(currentTask);
                    _dbContext.SaveChanges();
                }
            }

            return wo;
        }

        public bool SaveWorkOrdersAndAppointment(List<WorkOrderModel> workOrders, WorkOrderSchedulerModel woScheduler, string username)
        {
            //Note: using current existing code is not very performant, it's long to do everything...
            //Maybe we need to rewrote code to be specific for this!
            List<WorkOrderSchedulerModel> woSchedulers = new List<WorkOrderSchedulerModel>();

            using (IDbContextTransaction transaction = _dbContext.Database.BeginTransaction())
            {
                var workOrderSchedulerBl = _serviceProvider.GetService<IWorkOrderSchedulerBl>();

                try
                {
                    foreach (var workOrder in workOrders)
                    {
                        //Create appointment task
                        var wo = CreateWorkOrderAppointment(workOrder.Name, username, transaction);                        

                        //Get appointment tasks from config
                        var appointmentTasks = _workOrderConfig.GetAppointmentTasks(wo);

                        var task = FindActiveAppointmentTask(wo, appointmentTasks);

                        if (task != null) 
                        {
                            task.TaskStatus = _gevBl.GetOrCreate(TaskStatus.TaskCompleted.GetEnumName(), TaskStatus.TaskCompleted.ToStringValue());

                            //Cancel old appointment if any
                            try { workOrderSchedulerBl.CancelWorkOrderAppointment(workOrder.Name); } catch { }
                            _dbContext.SaveChanges();

                            //Book appointment
                            woScheduler.WorkOrderName = workOrder.Name;
                            woSchedulers.Add(workOrderSchedulerBl.Create(woScheduler, transaction));
                            _dbContext.SaveChanges();

                            //Detach model
                            var woDetach = _mapper.Map<WorkOrderModel>(wo);

                            //Update wo
                            woDetach.JsonData = workOrder.JsonData;
                            InnerUpdate(woDetach, username, true);

                            //Still have bug here when AwaitingAppointment with appointment task, this task is deleted instead of completed
                            woDetach = _dal.GetAllIQueryable(x => x.Name == workOrder.Name, noTracking: true).Include(x => x.Tasks).ThenInclude(x => x.TaskStatus).FirstOrDefault();
                            task = woDetach.Tasks.LastOrDefault();
                            CreateNextTask(workOrder.Name, task, username);
                            _dbContext.SaveChanges();
                        }
                        else
                        {
                            throw new Exception("Cannot find active appointment task.");
                        }
                    }
                    
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    foreach (var scheduler in woSchedulers)
                    {
                        try
                        {
                            workOrderSchedulerBl.Delete(scheduler.Name);
                        }
                        catch { }
                    }                    

                    transaction.Rollback();
                    return false;
                }
            }

            return true;
        }

        private bool AppointmentTaskAlreadyCreated(WorkOrderModel wo, List<ConfigTaskDto> appointmentTasks)
        {
            //Get new tasks
            var newTasks = wo.Tasks?.Where(x => x.TaskStatusString == TaskStatus.TaskNone.ToStringValue()).ToList();

            foreach (var task in newTasks)
            {
                if (appointmentTasks.Any(x => x.TaskName == task.WorkOrderWorkFlowTaskName))
                {
                    return true;
                }
            }

            return false;
        }

        private WorkOrderTaskModel FindActiveAppointmentTask(WorkOrderModel wo, List<ConfigTaskDto> appointmentTasks)
        {
            //Get new tasks
            var newTasks = wo.Tasks?.Where(x => x.TaskStatusString == TaskStatus.TaskNone.ToStringValue()).ToList();

            foreach (var task in newTasks)
            {
                if (appointmentTasks.Any(x => x.TaskName == task.WorkOrderWorkFlowTaskName))
                {
                    return task;
                }
            }

            return null;
        }

        //Mark as virtual for unittest override
        protected virtual void CreateTask(WorkOrderModel workOrder, WorkOrderTaskModel currentTask, string username, ExecutorModel.NextStepModel nextStep = null, WorkOrderConfigDto woConfig = null)
        {
            if (workOrder != null)
            {
                woConfig = woConfig ?? _workOrderConfig.GetWorkOrderConfig(workOrder);
                var config = _mapper.Map<ExecutorModel.ConfigModel>(woConfig);
                var wo = _mapper.Map<ExecutorModel.WorkOrderModel>(workOrder);
                var task = _mapper.Map<ExecutorModel.TaskModel>(currentTask);
                nextStep = nextStep ?? _executorService.Run(wo, task, config);

                if (nextStep.TaskTemplate == null)
                {
                    var msg = "Cannot create next task because the TaskTemplate was not found.";

                    if (woConfig.Conditions.Count == 0)
                    {
                        msg += " Possible reason: No condition was set for the task in CFG.";
                    }

                    LoggerUtil.LogError(msg);
                }
                else
                {
                    string woWebStatusName;

                    if (currentTask == null)
                    {
                        woWebStatusName = workOrder.StatusWeb.Name;
                    }
                    else
                    {
                        woWebStatusName = _workOrderConfig.GetCollectionItemById(Convert.ToInt64(nextStep.WorkorderStatus))?.AttributesSchemas.FirstOrDefault(x => x.Key == "GUID")?.Value;

                    }

                    var newTask = new WorkOrderTaskModel()
                    {
                        WorkOrderName = workOrder.Name,
                        WorkOrderTaskName = $"WOTask_{Guid.NewGuid()}_web",
                        WorkOrderWorkFlowName = woConfig.WorkOrderWorkflowName,
                        WorkOrderWorkFlowTaskName = nextStep.TaskTemplate.Name,
                        TaskStatusString = TaskStatus.TaskNone.ToStringValue(),
                        ActivityDetailString = ActivityDetail.WOTaskCreated.ToString(),
                        ActivityTypeString = ActivityType.ActivityTypeEventName.ToStringValue(),
                        StatusWebString = woWebStatusName
                    };

                    IWorkOrderTaskBlHelper workOrderTaskBlHelper = new WorkOrderTaskBlHelper(this, _versionBl, _historyBl, _gevBl, _mapper, _workOrderTaskDal, _workOrderStatusManager,_eventLogService);
                    workOrderTaskBlHelper.Create(new List<WorkOrderTaskModel> { newTask }, username, true, false);
                }
            }
        }

        private void SetWoAssignByStatus(WorkOrderModel wo, WorkOrderVersion version = null)
        {
            if (wo.StatusWeb?.Name == StatusWeb.ReadyForDispatch.ToString() || wo.StatusWebString == StatusWeb.ReadyForDispatch.ToString()
                || wo.StatusWebString == StatusWeb.UtilityCancel.ToStringValue() || wo.StatusWeb?.Name == StatusWeb.UtilityCancel.ToString()
                || wo.StatusWebString == StatusWeb.UtilityHold.ToStringValue() || wo.StatusWeb?.Name == StatusWeb.UtilityHold.ToString()
                || wo.StatusWebString == StatusWeb.ReadyForAudit.ToStringValue() || wo.StatusWeb?.Name == StatusWeb.ReadyForAudit.ToString())
            {

                wo.FsrName = string.Empty;
                wo.AssignedTo = string.Empty;
                wo.TeamDispatchId = null;
                wo.TeamDispatch = null;

                if (version != null)
                {
                    version.FsrName = string.Empty;
                    version.AssignedTo = string.Empty;
                    version.TeamDispatchId = null;
                }
            }
        }

        private void SetWorkOrderNameInVersion(List<WorkOrderVersion> versions)
        {
            versions.ForEach(x => x.WorkOrderName = GetOne(y => y.Id == x.WorkOrderId).Name);
        }

        public long GetTotalCount(string contractName, DateTime startD, DateTime endD, List<string> list, OlameterFramework.OUIFramework.RequestDto filter, InstallDateOption installDateOption = InstallDateOption.Custom)
        { return this._dal.GetTotalCount(contractName, startD, endD, list, filter, installDateOption); }

        public WorkOrderModel GetOne(Expression<Func<WorkOrderModel, bool>> expressionWhere) => base.GetOne(expressionWhere);

        public WorkOrderModel Delete(WorkOrderModel workOrder, string username, bool andSave = false, bool inTrans = true)
        {
            workOrder.FsrName = string.Empty;
            workOrder.AssignedTo = string.Empty;
            workOrder.TeamDispatchId = null;
            workOrder.TeamDispatch = null;

            WorkOrderModel model = base.Delete(workOrder, andSave);

            return model;
        }

        public List<string> GetPreFilterColumnValues(string contractName, DateTime startD, DateTime endD,
            string columnName, string columnFilterValue, InstallDateOption installDateOption = InstallDateOption.Custom)
        {
            if (string.IsNullOrEmpty(contractName))
            { throw new ArgumentException($"'{nameof(contractName)}' cannot be null or empty.", nameof(contractName)); }

            if (string.IsNullOrEmpty(columnName))
            { throw new ArgumentException($"'{nameof(columnName)}' cannot be null or empty.", nameof(columnName)); }

            return this._dal.GetPreFilterColumnValues(contractName, startD, endD, columnName, columnFilterValue, installDateOption);
        }

        public IEnumerable<string> GetLayerDataByPolygon(LayerDataByPolygonDto layerDataByPolygonDto)
        {
            return this._dal.GetLayerDataByPolygon(layerDataByPolygonDto);
        }

        public async Task<string> GetIFSByAssignedTo(string contract, DateTime startD, DateTime endD, List<string> woStatusNames, OlameterFramework.OUIFramework.RequestDto filter, InstallDateOption installDateOption = InstallDateOption.Custom)
        {
            var woNcount = this.GetAll(contract, startD, endD, woStatusNames, filter, installDateOption);

            var groupByAssignedTo = woNcount.GroupBy(k => k.AssignedTo);

            var list = new ConcurrentBag<(string AssignedTo, string TaskIds, DateTime LastDispatched)>();

            try
            {
                await Parallel.ForEachAsync(groupByAssignedTo, async (g, t) =>
                {
                    using (var context = new ApplicationDbContext())
                    {
                        var historyBL = new HistoryBl(context, new WoGenericEnumValuesBl(context)
                                                        , new AuditLogBl(context), new HistoryDal(context)
                                                        ,_mapper, _kafkaHelper);
                        var activityDetail = ActivityDetail.Workorderdispatched.ToStringValue();
                        var lastDispatched = await historyBL.GetHistories(h => h.ActivityDetail.Name == activityDetail && g.Select(s => s.Id).Contains(h.WorkOrderId)).OrderByDescending(o => o.TimeStamp).Select(s => s.TimeStamp).FirstOrDefaultAsync();

                        (string AssignedTo, string TaskIds, DateTime LastDispatched) tmp = (
                        GetAssignedToId(g.Key),
                        string.Join('|', g.Select(w => GetTaskId(w)).Where(w => w != null)),
                        lastDispatched);

                        list.Add(tmp);
                    }
                });
            }
            catch (Exception ex)
            {
                throw;
            }

            var sb = new StringBuilder();

            foreach (var (AssignedTo, TaskIds, _) in list.OrderByDescending(o => o.LastDispatched))
            {
                sb.AppendLine(AssignedTo);
                sb.AppendLine(TaskIds);
                sb.AppendLine();
            }

            if (sb.Length <= 0)
            {
                sb.AppendLine("No result!");
            }

            return sb.ToString();
        }

        public string GetRequestedFieldsOnly(List<string> requestedFields, string jsonData)
        {
            var filteredJson = "{}";

            if (requestedFields != null && requestedFields.Count > 0)
            {
                var propertiesToCopy = requestedFields.Select(x => x.Replace(">", ".")).ToList();

                JObject sourceObject = JObject.Parse(jsonData);
                JObject targetObject = new JObject();

                foreach (var propertyPath in propertiesToCopy)
                {
                    var tokens = propertyPath.Split('.');
                    JObject currentTarget = targetObject;

                    for (int i = 0; i < tokens.Length; i++)
                    {
                        try
                        {
                            // If it's the last token, set the value
                            if (i == tokens.Length - 1)
                            {
                                var value = sourceObject.SelectToken(propertyPath);
                                currentTarget[tokens[i]] = value;
                            }
                            else
                            {
                                // If the token doesn't exist, create a new JObject
                                if (!currentTarget.ContainsKey(tokens[i]))
                                {
                                    currentTarget[tokens[i]] = new JObject();
                                }
                                // Move to the next level in the target object
                                currentTarget = (JObject)currentTarget[tokens[i]];
                            }
                        }
                        catch (Exception e)//For debuging mapping
                        {
                            throw new Exception($"Error mapping fields: {tokens[i]}", e);
                        }
                    }
                }

                filteredJson = targetObject.ToString(Formatting.None);
            }

            return filteredJson;

        }

        public static string GetAssignedToId(string source)
        {
            try
            {
                var splited = source.Split('-');
                return source.Split('-').Last();
            }
            catch (Exception)
            {
                return source;
            }
        }

        public static string GetTaskId(WorkOrderModel model)
        {
            try
            {
                return model.JsonData.Get("WorkOrder.TaskId").ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

}
