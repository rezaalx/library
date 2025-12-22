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

        // History write batching: avoid one DbContext + SaveChanges per work order
        private const int HistoryBatchSize = 200;
        private const int HistoryMaxParallelBatches = 4;

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
            List<(WorkOrderModel Current, WorkOrderModel Previous)> historiesToWrite = new List<(WorkOrderModel, WorkOrderModel)>();

            HashSet<string> insideBlackOutNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> missingCycleRouteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            // Materialize once; the previous implementation re-materialized workOrders multiple times.
            List<WorkOrderModel> workOrderList = await ToListCompatAsync(workOrders);
            Dictionary<string, WorkOrderInstallDateDto> dtoByName = (data.WorkOrders ?? new List<WorkOrderInstallDateDto>())
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            if (data.Date > DateTime.MinValue)
            {
                // If these fields are missing, we can mark all as missing without iterating the blackout rules.
                if (cycleField == null || routeField == null)
                {
                    foreach (var wo in workOrderList)
                        missingCycleRouteNames.Add(wo.Name);
                }

                foreach (var group in workOrderList.GroupBy(x => new { x.ContractName, x.ServiceType }))
                {
                    // Blackouts are requested once per (contract, serviceType)
                    List<NextBlackOutDto> blackouts = _blackOutBl.GetAllBlackOuts(group.Key.ContractName, group.Key.ServiceType, data.Date);
                    if (blackouts == null || blackouts.Count == 0)
                        continue;

                    // Only check blackout conditions if we have the necessary fields.
                    if (cycleField == null || routeField == null)
                        continue;

                    foreach (var wo in group)
                    {
                        if (ValidateBlackOut(wo, blackouts, data.Date, cycleField, routeField))
                            insideBlackOutNames.Add(wo.Name);
                    }
                }
            }

            // NOTE: DbContext is not thread-safe, do NOT use AsParallel() here.
            List<WorkOrderModel> computedWorkOrders = new List<WorkOrderModel>(workOrderList.Count);
            foreach (var wo in workOrderList)
            {
                bool hasException = false;
                try
                {
                    WorkOrderModel previous = _mapper.Map(wo, new WorkOrderModel());

                    if (missingCycleRouteNames.Contains(wo.Name))
                    {
                        string woId = workOrderIdField == null ? wo.Name : wo.JsonData.Get($"{workOrderIdField.Category}.{workOrderIdField.Name}")?.ToString();
                        throw new InstallDateException(wo.Name, $"Work Order {woId} should have the field cycle/route");
                    }

                    //Important: Temp solution blackout should be in the message because the UI need it to filter the blocking exception
                    if (insideBlackOutNames.Contains(wo.Name))
                    {
                        string woId = workOrderIdField == null ? wo.Name : wo.JsonData.Get($"{workOrderIdField.Category}.{workOrderIdField.Name}")?.ToString();
                        exceptions.Add(new InstallDateException(wo.Name, $"Work Order {woId} is inside a blackout"));
                    }

                    wo.InstallDate = data.Date;

                    AddHistory(wo, previous, username, ref historiesToWrite);

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
                    if (!hasException)
                        computedWorkOrders.Add(wo);
                        

                    Interlocked.Increment(ref count);
                }
            }

            await _context.SaveChangesAsync();

            await WriteHistoriesBatchedAsync(historiesToWrite, username);
            
            st.Stop();
            LoggerUtil.LogDebug($"Async Parallel Duration: {st.Elapsed.TotalSeconds}");
            return new InstallDateResultDto
            {
                Computed = computedWorkOrders.ToArray(),
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

        protected virtual void AddHistory(WorkOrderModel current, WorkOrderModel previous, string username, ref List<(WorkOrderModel Current, WorkOrderModel Previous)> historiesToWrite)
        {
            historiesToWrite.Add((current, previous));
        }

        private async Task WriteHistoriesBatchedAsync(List<(WorkOrderModel Current, WorkOrderModel Previous)> histories, string username)
        {
            if (histories == null || histories.Count == 0)
                return;

            // Limit parallelism to avoid exhausting DB connections.
            using SemaphoreSlim gate = new SemaphoreSlim(HistoryMaxParallelBatches, HistoryMaxParallelBatches);
            List<Task> batchTasks = new List<Task>();

            for (int i = 0; i < histories.Count; i += HistoryBatchSize)
            {
                int start = i;
                int count = Math.Min(HistoryBatchSize, histories.Count - start);
                batchTasks.Add(Task.Run(async () =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        using ApplicationDbContext taskContext = new ApplicationDbContext();
                        HistoryBl historyBl = new HistoryBl(taskContext,
                            new WoGenericEnumValuesBl(taskContext),
                            new AuditLogBl(taskContext),
                            new HistoryDal(taskContext),
                            _mapper,
                            _kafkaHelper);

                        // Save once per batch (best-effort) instead of once per work order.
                        for (int j = 0; j < count; j++)
                        {
                            var item = histories[start + j];
                            bool andSave = (j == count - 1);
                            historyBl.Create(item.Current, item.Previous, username, andSave);
                        }
                    }
                    finally
                    {
                        gate.Release();
                    }
                }));
            }

            await Task.WhenAll(batchTasks);
        }

        /// <summary>
        /// Supports unit tests that use mocked/in-memory IQueryable providers that don't implement EF async.
        /// In production (EF Core provider), this uses ToListAsync.
        /// </summary>
        private static Task<List<T>> ToListCompatAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        {
            if (query is IAsyncEnumerable<T>)
                return EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken);

            return Task.FromResult(query.ToList());
        }
    }
}
