using SospectAPI.Helpers;
using Microsoft.EntityFrameworkCore;
using SospectAPI.Data;
using System.Text;
using System.Net;
using SospectAPI.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.Cookies;
using SospectAPI.Models;
using SospectAPI.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Amazon.S3;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.FileProviders;

namespace SospectAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment Environment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<INotificationService, NotificationHubService>();

            services.AddSingleton<ITraductorService, TraductorService>();

            services.AddDefaultAWSOptions(Configuration.GetAWSOptions());

            services.AddAWSService<IAmazonS3>();

            services.AddOptions<NotificationHubOptions>()
                .Configure(Configuration.GetSection("NotificationHub").Bind)
                .ValidateDataAnnotations();

            services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                    options.KnownProxies.Add(IPAddress.Parse("zz.ssss.sss.sss"));
                }
            );

            services.AddDbContext<DataContext>(cfg =>
            {
                cfg.UseNpgsql(Configuration.GetConnectionString("DefaultConnection"));
            });
            services.AddControllersWithViews();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; // set the default scheme to "Cookies"
            })
            .AddCookie()
            .AddJwtBearer(cfg =>
            {
                cfg.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = Configuration["Tokens:Issuer"],
                    ValidAudience = Configuration["Tokens:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Tokens:Key"]))
                };
            })
            .AddFacebook(fb =>
            {
                fb.AppId = "dsaDSAdsa";
                fb.AppSecret = "dsaDSAdsa";
                fb.Scope.Add("public_profile");
                fb.Scope.Add("email");
                fb.SaveTokens = true;
            })
            .AddGoogle(g =>
            {
                g.ClientId = "DSAdasdSAD.apps.googleusercontent.com";
                g.ClientSecret = "DSAdsaDSA";
                g.Scope.Add("profile");
                g.SaveTokens = true;
            })
            .AddApple(options =>
            {
                options.ClientId = Configuration["Apple:ClientId"];
                options.TeamId = Configuration["Apple:TeamId"];
                options.KeyId = Configuration["Apple:KeyId"];
                options.Scope.Add("name");  // Solicitar nombre
                options.Scope.Add("email"); // Solicitar correo electrónico
                options.UsePrivateKey(keyId =>
                {
                    // Asegúrate de que el archivo .p8 esté en tu directorio wwwroot
                    var physicalPath = Path.Combine(Environment.WebRootPath, "AuthKey_" + keyId + ".p8");
                    return new PhysicalFileInfo(new FileInfo(physicalPath));
                });
                options.SaveTokens = true;
            });

            services.AddScoped<IUserHelper, UserHelper>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseForwardedHeaders();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseForwardedHeaders();
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
