using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TFMS.Wpf
{
    public static class ServiceCollectionExtensionMethods
    {
        public static IServiceCollection AddViewModels(this IServiceCollection serviceCollection)
        {
            return serviceCollection
                .AddSingleton<TFMSViewModel>();
        }
    }
}
