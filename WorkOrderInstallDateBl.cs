using WFMS.WorkOrderExecution.Model;
using WFMS.WorkOrderExecution.Model.Dto;
using WFMS.WorkOrderExecution.BL.Utility;
using System.Threading.Tasks;
using System;
using WFMS.WorkOrderExecution.BL.Exceptions;
using System.Linq;
using AutoMapper;
using WFMS.WorkOrderExecution.DAL;
using System.Collections.Generic;
using OlameterFramework.OFramework.ConfigUtils;
using System.Diagnostics;
using Xamarin.Forms.Internals;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using OlameterFramework.EventBus.IntegrationEventLog;

namespace WFMS.WorkOrderExecution.BL.BusinessLayer
{
    public interface IInstallDateBl
    {
        public Task<InstallDateResultDto> SetInstallDate(InstallDateDto data,string username);
    }

    public class WorkOrderInstallDateBl : IInstallDateBl
    {
        protected readonly ApplicationDbContext _context;
        protected readonly IWorkOrderBlackOutBl _blackOutBl;
        protected readonly IMapper _mapper;
        private readonly IMessageIntegrationEventLogService _eventLogService;
        private readonly IKafkaHelper _kafkaHelper;

        public WorkOrderInstallDateBl(ApplicationDbContext context,IWorkOrderBlackOutBl blackOutBl,IMapper mapper,
            IMessageIntegrationEventLogService eventLogService, IKafkaHelper kafkaHelper)
        {
            _context = context;
            _blackOutBl = blackOutBl;
            _mapper = mapper;
            _eventLogService = eventLogService;
            _kafkaHelper = kafkaHelper;
        }

        public async Task<InstallDateResultDto> SetInstallDate(InstallDateDto data,string username)
        {
            Stopwatch st = new Stopwatch();
            st.Start();
            List<InstallDateException> exceptions = new List<InstallDateException>();
            ConcurrentBag<Task> historiesTask = new ConcurrentBag<Task>();
            List<WorkOrderInstallDateDto> insideBlackOutList = new List<WorkOrderInstallDateDto>();
            List<WorkOrderInstallDateDto> missingCycleRouteList = new List<WorkOrderInstallDateDto>();

            int count = 0;

            IQueryable<WorkOrderModel> workOrders = _context.Set<WorkOrderModel>().AsQueryable();
            
            List<ContractImportedFieldModel> neededField = _context.Set<ContractImportedFieldModel>()
                                                             .Where(x => data.WorkOrders.Select(y => y.ContractName).Contains(x.ContractName)
                                                                        && !x.IsDeleted
                                                                        && (x.Name == WorkOrderFieldName.WorkOrderIdFN
                                                                            || x.Name == WorkOrderFieldName.CycleFN
                                                                            || x.Name == WorkOrderFieldName.RouteFN)).ToList();

            ContractImportedFieldModel workOrderIdField = neededField.FirstOrDefault(x => x.Name == WorkOrderFieldName.WorkOrderIdFN);
            ContractImportedFieldModel cycleField = neededField.FirstOrDefault(x => x.Name == WorkOrderFieldName.CycleFN);
            ContractImportedFieldModel routeField = neededField.FirstOrDefault(x => x.Name == WorkOrderFieldName.RouteFN);

            if (data.IsAll)
                workOrders = workOrders.Where(x => data.WorkOrders.Select(y => y.ContractName).Contains(x.ContractName));
            else
                workOrders = workOrders.Where(x => data.WorkOrders.Select(y => y.Name).Contains(x.Name)).AsQueryable();


            if (data.Date > DateTime.MinValue)
            {
                workOrders.Select(x => new { x.ContractName, x.ServiceType }).Distinct().ToList().ForEach(x =>
                {
                    List<NextBlackOutDto> blackouts = _blackOutBl.GetAllBlackOuts(x.ContractName, x.ServiceType, data.Date);
                    workOrders.Where(y => y.ContractName == x.ContractName && y.ServiceType == x.ServiceType).ToList()
                              .ForEach(y => {
                                  WorkOrderInstallDateDto dto = data.WorkOrders.Single(z => z.Name == y.Name);

                                  if (cycleField == null || routeField == null)
                                      missingCycleRouteList.Add(dto);
                                  
                                  else if (ValidateBlackOut(y, blackouts,data.Date,cycleField,routeField))
                                      insideBlackOutList.Add(dto);
                              });
                });
            }

            WorkOrderModel[] computedWorkOrders = new WorkOrderModel[0];

            workOrders.AsParallel().ForEach(wo =>
            {
                bool hasException = false;
                try
                {
                    WorkOrderModel previous = _mapper.Map(wo, new WorkOrderModel());

                    if (missingCycleRouteList.Any(x => x.Name == wo.Name))
                        throw new InstallDateException(wo.Name, $"Work Order {wo.JsonData.Get($"{workOrderIdField.Category}.{workOrderIdField.Name}")} should have the field cycle/route");

                    //Important: Temp solution blackout should be in the message because the UI need it to filter the blocking exception
                    if (insideBlackOutList.Any(x => x.Name == wo.Name))
                        exceptions.Add(new InstallDateException(wo.Name, $"Work Order {wo.JsonData.Get($"{workOrderIdField.Category}.{workOrderIdField.Name}")} is inside a blackout"));

                    wo.InstallDate = data.Date;

                    AddHistory(wo, previous, username, ref historiesTask);

                }
                catch (InstallDateException e)
                {
                    hasException = true;
                    exceptions.Add(e);
                }
                catch (Exception e)
                {
                    hasException = true;
                    exceptions.Add(new InstallDateException("", e.Message));
                }
                finally
                {
                    if (!hasException) {
                        Array.Resize(ref computedWorkOrders,computedWorkOrders.Length+1);
                        computedWorkOrders[computedWorkOrders.Length - 1] = wo;
                    }
                        

                    Interlocked.Increment(ref count);
                }
            });

            await _context.SaveChangesAsync();

            Parallel.ForEachAsync(historiesTask,async (task,token) => task.Start());
            
            st.Stop();
            LoggerUtil.LogDebug($"Async Parallel Duration: {st.Elapsed.TotalSeconds}");
            return new InstallDateResultDto
            {
                Computed = computedWorkOrders,
                Error = exceptions.Select(e => new InstallDateErrorDto { 
                    Name = e.WorkOrderName,
                    Message = e.Message 
                })
            };
        }

        protected bool ValidateBlackOut(WorkOrderModel model, List<NextBlackOutDto> nextBlackOuts,DateTime date,ContractImportedFieldModel cycleField, ContractImportedFieldModel routeField) 
        {
            if (!nextBlackOuts.Any())
                return false;

            bool result = nextBlackOuts.Any(x => x.Cycle == model.JsonData.Get($"{cycleField.Category}.{cycleField.Name}")?.ToString() 
                                                && x.StartDate.Date <= date 
                                                && x.EndDate.Date >= date)
                          ||
                          nextBlackOuts.Any(x => x.Cycle == model.JsonData.Get($"{cycleField.Category}.{cycleField.Name}")?.ToString() 
                                                 && x.Route == model.JsonData.Get($"{routeField.Category}.{routeField.Name}")?.ToString() 
                                                 && x.StartDate.Date <= date 
                                                 && x.EndDate.Date >= date);
            
            return result;
        }

        protected virtual void AddHistory(WorkOrderModel current, WorkOrderModel previous, string username,ref ConcurrentBag<Task> historiesTask)
        {
            Task historyTask = new Task(() =>
            {
                using (ApplicationDbContext taskContext = new ApplicationDbContext())
                {
                    HistoryBl historyBl = new HistoryBl(taskContext
                                            , new WoGenericEnumValuesBl(taskContext)
                                            , new AuditLogBl(taskContext)
                                            , new HistoryDal(taskContext),_mapper, _kafkaHelper);

                    historyBl.Create(current, previous, username, true);
                }
            });

            historiesTask.Add(historyTask);
        }
    }
}
