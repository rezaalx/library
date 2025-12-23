using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using OlameterFramework.DatabaseUtilities;
using OlameterFramework.OFramework.ConfigUtils;
using OlameterFramework.OFramework.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OlameterFramework.EventBus.IntegrationEventLog;
using WFMS.WorkOrderExecution.BL.BusinessLayer;
using WFMS.WorkOrderExecution.DAL;
using WFMS.WorkOrderExecution.Model;
using WFMS.WorkOrderExecution.Model.Dto;
using WFMS.WorkOrderExecution.Model.MappingConfigurations;
using WFMS.WorkOrderExecution.BL.Utility;

namespace WFMS.WorkOrderExecution.Tests
{
    [TestClass]
    public class WorkOrderInstallDateBlTests
    {
        private readonly IInstallDateBl _bl;
        private readonly ApplicationDbContext _context;
        private readonly IWorkOrderBlackOutBl _blackoutBL;
        private readonly IMapper _mapper;
        private readonly IMessageIntegrationEventLogService _eventLogService;
        private IKafkaHelper _kafkaHelper;

        public WorkOrderInstallDateBlTests()
        {
            var mappingConfig = new MapperConfiguration(mc =>
            {
                mc.AddProfile(new WorkOrderModelProfile());
            });

            _mapper = mappingConfig.CreateMapper();
            _blackoutBL = Substitute.For<IWorkOrderBlackOutBl>();
            _context = Substitute.For<ApplicationDbContext>();
            _kafkaHelper = Substitute.For<IKafkaHelper>();
            DbSet<WorkOrderModel> workOrders = InitializeMockDBSet(new List<WorkOrderModel> {
                new WorkOrderModel{
                    Name = "1",
                    ServiceType = "Power",
                    ContractName = "A",
                    JsonData = JsonDocument.Parse(new {Asset = new { InstallDate = string.Empty}, WorkOrder = new { WorkOrderId = "1",Cycle = "1",Route = "a"} }.ToJsonString())
                },
                new WorkOrderModel{
                    Name = "2",
                    ServiceType = "Power",
                    ContractName = "A",
                    JsonData = JsonDocument.Parse(new {Asset = new { InstallDate = string.Empty}, WorkOrder = new { WorkOrderId = "1",Cycle = "2",Route = "a"} }.ToJsonString())
                },
                new WorkOrderModel{
                    Name = "3",
                    ServiceType = "Power",
                    ContractName = "A",
                    JsonData = JsonDocument.Parse(new {Asset = new { InstallDate = string.Empty}, WorkOrder = new { WorkOrderId = "1"} }.ToJsonString())
                },
                new WorkOrderModel{
                    Name = "4",
                    ServiceType = "Power",
                    ContractName = "A",
                    JsonData = JsonDocument.Parse(new { WorkOrder = new { WorkOrderId = "1",Cycle = "2",Route = "a"} }.ToJsonString())
                }
            }.AsQueryable());
            _context.Set<WorkOrderModel>().Returns(workOrders);
            _context.Set<WorkOrderModel>().AsQueryable().Returns(workOrders);

            DbSet<ContractImportedFieldModel> contractImportedFields = InitializeMockDBSet(new List<ContractImportedFieldModel> {
                new ContractImportedFieldModel { Name = WorkOrderFieldName.InstallDateFN, Category="Asset",ContractName= "A" },
                new ContractImportedFieldModel { Name = WorkOrderFieldName.WorkOrderIdFN, Category="WorkOrder",ContractName= "A" },
                new ContractImportedFieldModel { Name = WorkOrderFieldName.CycleFN, Category="WorkOrder",ContractName= "A" },
                new ContractImportedFieldModel { Name = WorkOrderFieldName.RouteFN, Category="WorkOrder",ContractName= "A" }
                }.AsQueryable());

            _context.Set<ContractImportedFieldModel>().Returns(contractImportedFields);
            _context.Set<ContractImportedFieldModel>().AsQueryable().Returns(contractImportedFields);

            _blackoutBL.GetAllBlackOuts(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>()).Returns(new List<NextBlackOutDto> {
                new NextBlackOutDto{
                    ContractId = "A",
                    Cycle = "2",
                    Route = "a",
                    ServiceType = "Power",
                    EndDate = DateTime.Now.AddDays(2).Date,
                    StartDate = DateTime.Now.Date,
                }
            });

            _bl = new WorkOrderInstallDateBlTest(_context, _blackoutBL, _mapper,_eventLogService, _kafkaHelper);
        }

        [TestInitialize]
        public void Initialize()
        {
        }

        private DbSet<T> InitializeMockDBSet<T>(IQueryable<T> data) where T : BaseModel
        {
            DbSet<T> mockSet = Substitute.For<DbSet<T>, IQueryable<T>>();
            ((IQueryable<T>)mockSet).Provider.Returns(data.Provider);
            ((IQueryable<T>)mockSet).Expression.Returns(data.Expression);
            ((IQueryable<T>)mockSet).ElementType.Returns(data.ElementType);
            ((IQueryable<T>)mockSet).GetEnumerator().Returns(data.GetEnumerator());

            return mockSet;
        }

        [TestMethod]
        public async Task SetDateNoBlackOut_Succeed() {
            InstallDateResultDto result = await _bl.SetInstallDate(new InstallDateDto
            {
                WorkOrders = new List<WorkOrderInstallDateDto> {
                                new WorkOrderInstallDateDto {
                                    Name = "1",
                                    ContractName= "A",
                                    ServiceType = "Power"
                                } },
                Date = DateTime.Now.Date,
                IsAll = false,
            }, "test");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Computed.Count() > 0);
            Assert.IsTrue(!result.Error.Any());
        }

        [TestMethod]
        public async Task SetDateWithBlackOut_Succeed()
        {
            InstallDateResultDto result = await _bl.SetInstallDate(new InstallDateDto
            {
                WorkOrders = new List<WorkOrderInstallDateDto> {
                                new WorkOrderInstallDateDto {
                                    Name = "2",
                                    ContractName= "A",
                                    ServiceType = "Power"
                                } },
                Date = DateTime.Now.Date,
                IsAll = false,
            }, "test");

            Assert.IsNotNull(result);
           // Assert.IsTrue(result.Computed.Count() > 0);
            Assert.IsTrue(result.Error.Any());
        }

        // For now Cycle and Route path are hardcoded, should be change for the path with ContractImportedField
        [Ignore]
        [TestMethod]
        public async Task SetDateWithoutCycleAndRouteInJson_Succeed()
        {
            InstallDateResultDto result = await _bl.SetInstallDate(new InstallDateDto
            {
                WorkOrders = new List<WorkOrderInstallDateDto> {
                                new WorkOrderInstallDateDto {
                                    Name = "3",
                                    ContractName= "A",
                                    ServiceType = "Power"
                                } },
                Date = DateTime.Now.Date,
                IsAll = false,
            }, "test");

            Assert.IsNotNull(result);
           // Assert.IsTrue(result.Computed.Count() > 0);
            Assert.IsTrue(result.Error.Any());
        }

        [TestMethod]
        public async Task SetDateWithoutInstallDateInJson_Succeed()
        {
            InstallDateResultDto result = await _bl.SetInstallDate(new InstallDateDto
            {
                WorkOrders = new List<WorkOrderInstallDateDto> {
                                new WorkOrderInstallDateDto {
                                    Name = "4",
                                    ContractName= "A",
                                    ServiceType = "Power"
                                } },
                Date = DateTime.Now.Date,
                IsAll = false,
            }, "test");

            Assert.IsNotNull(result);
            //Assert.IsTrue(result.Computed.Count() > 0);
            Assert.IsTrue(result.Error.Any());
        }

        [TestMethod]
        public async Task SetDate_Performance_1000Records()
        {
            int count = 1000;
            var workOrderList = new List<WorkOrderModel>();
            var workOrderDtos = new List<WorkOrderInstallDateDto>();

            for (int i = 0; i < count; i++)
            {
                string name = $"WO_{i}";
                workOrderList.Add(new WorkOrderModel
                {
                    Name = name,
                    ServiceType = "Power",
                    ContractName = "A",
                    JsonData = JsonDocument.Parse(new { Asset = new { InstallDate = string.Empty }, WorkOrder = new { WorkOrderId = name, Cycle = "1", Route = "a" } }.ToJsonString())
                });

                workOrderDtos.Add(new WorkOrderInstallDateDto
                {
                    Name = name,
                    ContractName = "A",
                    ServiceType = "Power"
                });
            }

            DbSet<WorkOrderModel> workOrders = InitializeMockDBSet(workOrderList.AsQueryable());
            _context.Set<WorkOrderModel>().Returns(workOrders);
            _context.Set<WorkOrderModel>().AsQueryable().Returns(workOrders);

            InstallDateResultDto result = await _bl.SetInstallDate(new InstallDateDto
            {
                WorkOrders = workOrderDtos,
                Date = DateTime.Now.Date,
                IsAll = false,
            }, "test");

            Assert.IsNotNull(result);
            Assert.AreEqual(count, result.Computed.Length);
            Assert.IsFalse(result.Error.Any());
        }
    }

    public class WorkOrderInstallDateBlTest : WorkOrderInstallDateBl
    {
        public WorkOrderInstallDateBlTest(ApplicationDbContext context, IWorkOrderBlackOutBl blackOutBl, IMapper mapper,IMessageIntegrationEventLogService eventLogService, IKafkaHelper kafkaHelper) : base(context, blackOutBl, mapper,eventLogService, kafkaHelper)
        {
        }

        protected override void AddHistory(WorkOrderModel current, WorkOrderModel previous, string username, ref ConcurrentBag<HistoryData> historyDataList)
        {
            LoggerUtil.LogDebug("Add History in Test");
        } 
    }
}
