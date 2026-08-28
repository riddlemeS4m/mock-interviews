using System.Globalization;
using MockInterviews.Models.Entities;
using MockInterviews.Services;

namespace MockInterviews.Data.Seeds
{
    //not really a constant class
    //probably should have a seed directory
    public class TimeslotSeed
    {
        public static int MaxSignups { get; set; } = 0;
        public static readonly string[] Times = {
            "8:00 AM",
            "8:30 AM",
            "9:00 AM",
            "9:30 AM",
            "10:00 AM",
            "10:30 AM",
            "11:00 AM",
            "11:30 AM",
            "12:00 PM",
            "12:30 PM",
            "1:00 PM",
            "1:30 PM",
            "2:00 PM",
            "2:30 PM",
            "3:00 PM",
            "3:30 PM",
            "4:00 PM",
            "4:30 PM",
        };

        public static readonly bool[] Student = { false, false, true, false, true, false, true, false, false, false, true, false, true, false, true, false, false, false };
        public static readonly bool[] Interviewer = { false, false, true, false, true, false, true, false, false, false, true, false, true, false, true, false, false, false };

        public static async Task SeedTimeslots(EventService eventService, TimeslotService timeslotService)
        {
            var dates = await eventService.GetAllAsync();

            if (dates.Any())
            {
                var times = await timeslotService.GetAllAsync();
                var timeslots = SeedTimeslots(dates);

                foreach (var timeslot in timeslots)
                {
                    if (!times.Any(x => x.Time.TimeOfDay == timeslot.Time.TimeOfDay && x.Event.Date == timeslot.Event.Date))
                    {
                        await timeslotService.AddAsync(timeslot);
                    }
                }
            }
        }

        public static async Task SeedTimeslots(TimeslotService timeslotService, Event theEvent)
        {
            var timeslots = SeedTimeslots(new List<Event> { theEvent });

            await timeslotService.AddRange(timeslots);
        }

        private static List<Timeslot> SeedTimeslots(IEnumerable<Event> dates)
        {
            var timeslots = new List<Timeslot>();

            foreach (var date in dates)
            {
                for (int i = 0; i < Times.Length; i++)
                {
                    var timeslot = new Timeslot()
                    {
                        Time = DateTime.SpecifyKind(
                            DateTime.ParseExact(Times[i], "h:mm tt", CultureInfo.InvariantCulture),
                            DateTimeKind.Utc),
                        Event = date,
                        EventId = date.Id,
                        IsActive = true,
                        IsVolunteer = true,
                        IsInterviewer = Interviewer[i],
                        IsStudent = Student[i],
                        MaxSignUps = MaxSignups
                    };
                    timeslots.Add(timeslot);
                }
            }

            return timeslots;
        }
    }
}
