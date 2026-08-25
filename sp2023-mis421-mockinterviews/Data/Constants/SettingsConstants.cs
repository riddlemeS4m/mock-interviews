using sp2023_mis421_mockinterviews.Models.Entities;

namespace sp2023_mis421_mockinterviews.Data.Constants
{
    public class SettingsConstants
    {
        private static readonly List<Setting> Settings = new(){
            new Setting { Name = ZoomLink.Name, Value = ZoomLink.DefaultValue },
            new Setting { Name = ZoomLinkVisible.Name, Value = ZoomLinkVisible.DefaultValue },
            new Setting { Name = DisruptionBanner.Name, Value = DisruptionBanner.DefaultValue },
            new Setting { Name = InterviewIndexHours.Name, Value = InterviewIndexHours.DefaultValue },
            new Setting { Name = MaximumTimeslotSignups.Name, Value = MaximumTimeslotSignups.DefaultValue },
            new Setting { Name = AutomaticallyReleaseTimeslots.Name, Value = AutomaticallyReleaseTimeslots.DefaultValue },
            new Setting { Name = MockInterviewManual.Name, Value = MockInterviewManual.DefaultValue },
            new Setting { Name = GuestParkingPass.Name, Value = GuestParkingPass.DefaultValue }
        };

        public static IEnumerable<Setting> GetSettings()
        {
            return Settings;
        }
        
        public class ZoomLink
        {
            public const string Name = "zoom_link";
            public const string DefaultValue = "https://mockinterviews.uamishub.com";
        }

        public class ZoomLinkVisible
        {
            public const string Name = "zoom_link_visible";
            public const string DefaultValue = "0";
        }

        public class DisruptionBanner
        {
            public const string Name = "disruption_banner";
            public const string DefaultValue = "0";
        }

        public class InterviewIndexHours
        {
            public const string Name = "interview_index_hours";
            public const string DefaultValue = "3";
        }

        public class MaximumTimeslotSignups
        {
            public const string Name = "maximum_timeslot_signups";
            public const string DefaultValue = "8";
        }

        public class AutomaticallyReleaseTimeslots
        {
            public const string Name = "automatically_release_timeslots";
            public const string DefaultValue = "0";
        }

        public class MockInterviewManual
        {
            public const string Name = "mock_interview_manual";
            public const string DefaultValue = "";
        }

        public class GuestParkingPass
        {
            public const string Name = "guest_parking_pass";
            public const string DefaultValue = "";
        }
    }
}
