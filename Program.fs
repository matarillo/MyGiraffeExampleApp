module MySample.App

open System
open System.IO
open System.Net
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.Server.Kestrel.Core
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
        GET >=>
            choose [
                route "/" >=> Hello.helloHandler "world"
                routef "/hello/%s" Hello.helloHandler
                route "/query" >=> Hello.queryHandler
                route "/blogcard" >=> BlogCard.blogCardHandler
            ]
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureKestrel (ctx : WebHostBuilderContext) (options : KestrelServerOptions) =
    let forDevelopment (options : KestrelServerOptions) =
        let useHttps (x : ListenOptions) =
            x.UseHttps()
                |> ignore
        options.Listen(IPAddress.Loopback, 5000)
        options.Listen(IPAddress.Loopback, 5001, useHttps)

    let forProduction (options : KestrelServerOptions) =
        options.Listen(IPAddress.Any, 5000)

    match ctx.HostingEnvironment.EnvironmentName with
    | "Development" -> forDevelopment options
    | _             -> forProduction options

let configureApp (ctx : WebHostBuilderContext) (app : IApplicationBuilder) =
    let configureCors (builder : CorsPolicyBuilder) =
        builder.WithOrigins("*")
            .AllowAnyMethod()
            .AllowAnyHeader()
            |> ignore

    let forDevelopment (app : IApplicationBuilder) =
        app.UseDeveloperExceptionPage()
            .UseHttpsRedirection()
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(webApp)

    let forProduction (app : IApplicationBuilder) =
        app.UseForwardedHeaders()
            .UseGiraffeErrorHandler(errorHandler)
            .UseCors(configureCors)
            .UseStaticFiles()
            .UseGiraffe(webApp)

    match ctx.HostingEnvironment.EnvironmentName with
    | "Development" -> forDevelopment app
    | _             -> forProduction app

let configureServices (ctx : WebHostBuilderContext) (services : IServiceCollection) =
    let forDevelopment (services : IServiceCollection) =
        services.AddCors()
            .AddGiraffe()
            |> ignore

    let forProduction (services : IServiceCollection) =
        let configureForwardedHeaders (options : ForwardedHeadersOptions) =
            options.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto
        services.Configure(configureForwardedHeaders)
            .AddCors()
            .AddGiraffe()
            |> ignore

    match ctx.HostingEnvironment.EnvironmentName with
    | "Development" -> forDevelopment services
    | _             -> forProduction services

let configureLogging (builder : ILoggingBuilder) =
    builder.AddFilter(fun l -> l.Equals LogLevel.Error)
        .AddConsole()
        .AddDebug()
        |> ignore

[<EntryPoint>]
let main args =
    let contentRoot = Directory.GetCurrentDirectory()
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .UseKestrel(configureKestrel)
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()
        .Run()
    0
