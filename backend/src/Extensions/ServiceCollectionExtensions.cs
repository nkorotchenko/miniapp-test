namespace WebAppBot.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static T? AddSettings<T>(this IServiceCollection services, IConfiguration configuration, string sectionName)
        {
            var settings = configuration.GetSection(sectionName).Get<T>();
            if (settings != null)
            {
                services.AddSingleton(settings.GetType(), settings);
            }

            return settings;
        }
    }
}
