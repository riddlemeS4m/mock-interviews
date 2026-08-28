global using System.Net;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;
global using System.Security.Claims;
global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Mvc.Testing;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Logging;
global using MockInterviews.Data.Constants;
global using MockInterviews.Data.Contexts;
global using MockInterviews.Models.Entities;
global using MockInterviews.Models.Identity;
global using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
