using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EventBusRabbitMQ;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ordering.API.Extensions;
using Ordering.API.RabbitMQ;
using Ordering.Application.Handlers;
using Ordering.Core.Repositories;
using Ordering.Core.Repositories.Base;
using Ordering.Infrastructure.Data;
using Ordering.Infrastructure.Repository;
using Ordering.Infrastructure.Repository.Base;
using RabbitMQ.Client;

namespace Ordering.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            //sql server connection
            services.AddDbContext<OrderContext>(c =>
                c.UseSqlServer(Configuration.GetConnectionString("OrderConnection")), ServiceLifetime.Singleton);

            // Infrastructure Layer dependency injection
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped(typeof(IOrderRepository), typeof(OrderRepository));
            services.AddTransient<IOrderRepository, OrderRepository>();

            // Add AutoMapper
            services.AddAutoMapper(typeof(Startup));

            // Add MediatR
            services.AddMediatR(typeof(CheckoutOrderHandler).GetTypeInfo().Assembly);

            services.AddSingleton<IRabbitMQConnection>(sp =>
            {
                var factory = new ConnectionFactory()
                {
                    HostName = Configuration["EventBus:HostName"]
                };

                if (!string.IsNullOrEmpty(Configuration["EventBus:UserName"]))
                {
                    factory.UserName = Configuration["EventBus:UserName"];
                }

                if (!string.IsNullOrEmpty(Configuration["EventBus:Password"]))
                {
                    factory.Password = Configuration["EventBus:Password"];
                }

                return new RabbitMQConnection(factory);
            });

            services.AddSingleton<EventBusRabbitMQConsumer>();

            //swagger
            services.AddSwaggerDocument(config =>
            {
                config.PostProcess = doc =>
                {
                    doc.Info.Version = "v1";
                    doc.Info.Title = "Ordering API";
                    doc.Info.Description = "Ordering API for Microservices";
                    doc.Info.Contact = new NSwag.OpenApiContact
                    {
                        Name = "Anar Baydamirov",
                        Email = "anarbaydamir@gmail.com",
                        Url = "https://github.com/anarbaydamirov"
                    };
                };
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            //middleware for rabbit mq
            app.UseRabbitListener();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //swagger
            app.UseOpenApi();
            app.UseSwaggerUi3();
        }
    }
}