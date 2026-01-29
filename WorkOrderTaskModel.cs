using GenericEnumValues.Model;
using OlameterFramework.DatabaseUtilities;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace WFMS.WorkOrderExecution.Model
{
    public class WorkOrderTaskModel : BaseModel
    {
        public override string DefaultResourceIdentifier => "WOT";

        public long WorkOrderId { get; set; }
        public WorkOrderModel WorkOrder { get; set; }

        public string WorkOrderTaskName { get; set; }
        public string WorkOrderWorkFlowTaskName { get; set; }

        public string WorkOrderWorkFlowName { get; set; }

        public GenericEnumValue TaskStatus { get; set; }

        public DateTime CreatedOn { get; set; }

        public string Comment { get; set; }

        public string TaskCategoryPayroll { get; set; }
        
        [NotMapped] private string _taskStatusString;

        [NotMapped]
        public string TaskStatusString
        {
            get
            {
                if (TaskStatus != null)
                {
                    return TaskStatus.Name;
                }

                return _taskStatusString;
            }
            set
            {
                _taskStatusString = value;
            }
        }

        [NotMapped]
        public string ActivityDetailString { get; set; }

        [NotMapped]
        public string ActivityTypeString { get; set; }

        [NotMapped]
        public string WorkOrderName { get; set; }

        [NotMapped]
        public bool IsSuccessfull { get; set; }

        [NotMapped]
        public JsonDocument JsonData { get; set; }

        [NotMapped]
        public string StatusWebString { get; set; }
        public DateTimeOffset EventDate { get; set; }
        public string CompletedBy { get; set; }
        [NotMapped] 
        public string TaskTitle;
    }
}
