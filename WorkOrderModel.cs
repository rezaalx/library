using GenericEnumValues.Model;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using OlameterFramework.DatabaseUtilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace WFMS.WorkOrderExecution.Model
{
    public class WorkOrderModel : BaseModel
    {
        public WorkOrderModel()
        {
            Versions = new List<WorkOrderVersion>();
            Tasks = new List<WorkOrderTaskModel>();
        }

        //https://www.npgsql.org/efcore/mapping/json.html?tabs=data-annotations%2Cpoco
        public JsonDocument JsonData { get; set; }

        public DateTime CreatedOn { get; set; }
        public string AuditorName { get; set; }
        public bool Audited { get; set; }
        public bool AuditMode { get; set; }
        public bool Active { get; set; }
        public DateTime? StartDateBlackout { get; set; }
        public DateTime? EndDateBlackout { get; set; }
        public int BlackoutRemainingDays { get; set; }
        public bool MexFlag { get; set; }
        public DateTimeOffset? CompletedOn { get; set; }

        #region for internal workflow
        public Geometry Position { get; set; }
        public Geometry Geofence { get; set; }
        public int PositionSRID { get; set; }
        public string ContractName { get; set; }
        public string ServiceType { get; set; }

        public List<WorkOrderVersion> Versions { get; set; }

        public List<WorkOrderTaskModel> Tasks { get; set; }

        public long? TeamDispatchId { get; set; }
        public TeamDispatchModel? TeamDispatch { get; set; }

        public ICollection<FileLocationModel> FileLocations { get; set; }

        public DownloadWOTracking DownloadWOTracking { get; set; }

        [Comment("All work orders that are dispatched/assigned by the dispatcher will have a fsrId associated.")]
        public string FsrName { get; set; }

        public string AssignedTo { get; set; }

        public DateTime? AppointmentDate { get; set; }
        public DateTime? InstallDate { get; set; }

        [Required]
        public GenericEnumValue StatusWeb { get; set; }

        public DateTimeOffset? StatusLastChange { get; set; }

        [Required]
        public GenericEnumValue PostProcessStatus { get; set; }

        [Required]
        public long StatusWebId { get; set; }

        [Required] public long PostProcessStatusId { get; set; }
        #endregion

        [NotMapped] public string ContractTitle { get; set; }
        [NotMapped] public string ContractTimeZone { get; set; }
        [NotMapped] public override string DefaultResourceIdentifier { get => "WO"; }
        [NotMapped] private string _statuswebString;
        [NotMapped]
        public string StatusWebString
        {
            get
            {
                if (StatusWeb != null)
                {
                    return StatusWeb.Name;
                }

                return _statuswebString;
            }
            set
            {
                _statuswebString = value;
            }
        }

        [NotMapped]
        //Used on update to set new wo status. Status may not change if status rules are not meet.
        public string NewStatusWebString { get; set; }

        [NotMapped] private string _postProcessStatusString;
        [NotMapped]
        public string PostProcessStatusString
        {
            get
            {
                if (this.PostProcessStatus != null)
                {
                    return PostProcessStatus.Name;
                }

                return _postProcessStatusString;
            }
            set
            {
                _postProcessStatusString = value;
            }
        }        

        [NotMapped]
        public bool IsInBlackOut
        {
            get
            {
                var date = DateTime.UtcNow;

                return (StartDateBlackout.HasValue &&
                EndDateBlackout.HasValue &&
                date.Date >= StartDateBlackout.Value.Date &&
                date.Date <= EndDateBlackout.Value.Date);
            }
        }

        /// <summary>
        /// this is not the same name in the json and in the destination object
        /// </summary>
        /// <summary>
        /// this is not the same name in the json and in the destination object
        /// </summary>
        [NotMapped] public const string SRID = "SRID";
    }
    public static class WorkOrderFieldName{
        public static string WorkOrderIdFN = "WorkOrderId";
        public static string LongitudeFN = "Longitude";
        public static string LatitudeFN = "Latitude";
        public static string AddressFN = "Address";
        public static string InstallDateFN = "InstallDate";
        public static string CycleFN = "Cycle";
        public static string RouteFN = "Route";
        public static string CallConsentFN = "CallConsent";
        public static string DoNotCallFN = "DoNotCall";
        public static string CellPhoneNoFN = "CellPhoneNo";
        public static string CustomerNameFN = "CustomerName";
        public static string RecordTypeFN = "RecordType";
        public static string InstallFlagFN = "InstallFlag";
        public static string AMInclusionExclusionFN = "AMIInclusionExclusion";
        public static string NotesFN = "CustomerCallNotes";
    }
}