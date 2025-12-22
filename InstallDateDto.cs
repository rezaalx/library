using System;
using System.Collections.Generic;

namespace WFMS.WorkOrderExecution.Model.Dto
{
    public class WorkOrderInstallDateDto 
    {
        public string Name { get; set; }
        public string ContractName { get; set; }
        public string ServiceType { get; set; }
    }

    public class InstallDateDto
    {
        public List<WorkOrderInstallDateDto> WorkOrders { get; set; }
        public DateTime Date { get; set; }
        public bool IsAll { get; set; }
    }

    public class InstallDateResultDto 
    { 
        public WorkOrderModel[] Computed { get; set; }
        public IEnumerable<InstallDateErrorDto> Error { get; set; }
    }

    public class InstallDateErrorDto
    { 
        public string Name { get; set; }
        public string Message { get; set; }
    }
}
