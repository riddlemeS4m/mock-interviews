using System.Reflection;
using sp2023_mis421_mockinterviews.Data.Contexts;
using sp2023_mis421_mockinterviews.Interfaces.IServices;

namespace sp2023_mis421_mockinterviews.Services.SignupDb
{
    public class SignupDbServiceFactory : ISignupDbServiceFactory
    {
        public EventService Events { get; set; }
        public InterviewService Interviews { get; set; }
        public InterviewerLocationService InterviewerLocations { get; set; }
        public InterviewerSignupService InterviewerSignups { get; set; }
        public InterviewerTimeslotService InterviewerTimeslots { get; set; }
        public SettingsService Settings { get; set; }
        public TimeslotService Timeslots { get; set; }

        public SignupDbServiceFactory(IServiceProvider serviceProvider)
        {
            foreach (var property in typeof(SignupDbServiceFactory).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var serviceType = property.PropertyType;
                var createdService = CreateService(serviceType, serviceProvider);
                property.SetValue(this, createdService);
            }            
        }

        private object CreateService(Type serviceType, IServiceProvider serviceProvider)
        {
            var dbContext = serviceProvider.GetRequiredService<MockInterviewsDbContext>();
            var loggerType = typeof(ILogger<>).MakeGenericType(serviceType);
            var logger = serviceProvider.GetService(loggerType);

            return Activator.CreateInstance(serviceType, dbContext, logger);
        }
    }
}