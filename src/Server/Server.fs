module Server

open FSharp.Control.Tasks
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Identity.Web
open Saturn
open Giraffe
open Microsoft.AspNetCore.Http

open Shared
open Microsoft.Extensions.Hosting
open System

type Storage () =
    let todos = ResizeArray<_>()

    member __.GetTodos () =
        List.ofSeq todos

    member __.AddTodo (todo: Todo) =
        if Todo.isValid todo.Description then
            todos.Add todo
            Ok ()
        else Error "Invalid todo"

let storage = Storage()

storage.AddTodo(Todo.create "Create new SAFE project") |> ignore
storage.AddTodo(Todo.create "Write your app") |> ignore
storage.AddTodo(Todo.create "Ship it !!!") |> ignore

let todosApi ctx =
    { getTodos = fun () -> async { return storage.GetTodos() }
      addTodo =
        fun todo -> async {
            match storage.AddTodo todo with
            | Ok () -> return todo
            | Error e -> return failwith e
        } }

let buildRemotingApi api next ctx = task {
    let handler =
        Remoting.createApi()
        |> Remoting.withRouteBuilder Route.builder
        |> Remoting.fromValue (api ctx)
        |> Remoting.buildHttpHandler
    return! handler next ctx }

let authScheme = "AzureAD"
let isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") = Environments.Development;

let noAuthenticationRequired nxt ctx = task { return! nxt ctx }

let requireLoggedIn : HttpFunc -> HttpContext -> HttpFuncResult =
    //if isDevelopment then
    //    noAuthenticationRequired
    //else
        requiresAuthentication (RequestErrors.UNAUTHORIZED authScheme "My Application" "You must be logged in.")

let authChallenge : HttpFunc -> HttpContext -> HttpFuncResult =
    //if isDevelopment then
    //    noAuthenticationRequired
    //else
        requiresAuthentication (Auth.challenge authScheme)

let routes =
    choose [
        route "/" >=> authChallenge >=>  htmlFile "public/app.html"
        subRoute "/api" (requireLoggedIn >=> buildRemotingApi todosApi)
    ]

let configureServices (services : IServiceCollection) =

    let config = services.BuildServiceProvider().GetService<IConfiguration>()

    services
        .AddMicrosoftIdentityWebAppAuthentication (config, openIdConnectScheme = authScheme)
        |> ignore

    services

let configureApp (app:IApplicationBuilder) =
    app
        .UseAuthentication()
        .UseHsts()
        .UseHttpsRedirection()

let app =
    application {
        url "http://0.0.0.0:8085"
        service_config configureServices
        app_config configureApp
        use_router routes
        memory_cache
        use_static "public"
        use_gzip
    }

Application.run app
