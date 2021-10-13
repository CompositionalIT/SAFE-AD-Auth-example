module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Microsoft.Identity.Web
open Giraffe
open Shared
open System
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Authentication

type Environment with
    static member IsDevelopmentEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") = Environments.Development


type Storage() =
    let todos = ResizeArray<_>()

    member __.GetTodos() = List.ofSeq todos

    member __.AddTodo(todo: Todo) =
        if Todo.isValid todo.Description then
            todos.Add todo
            Ok()
        else
            Error "Invalid todo"

let storage = Storage()

storage.AddTodo(Todo.create "Create new SAFE project")
|> ignore

storage.AddTodo(Todo.create "Write your app")
|> ignore

storage.AddTodo(Todo.create "Ship it !!!")
|> ignore

let todosApi =
    { getTodos = fun () -> async { return storage.GetTodos() }
      addTodo =
          fun todo ->
              async {
                  match storage.AddTodo todo with
                  | Ok () -> return todo
                  | Error e -> return failwith e
              } }

let webApp =
    Remoting.createApi ()
    |> Remoting.fromValue todosApi
    |> Remoting.buildHttpHandler

let authScheme = "AzureAD"

let logout (next : HttpFunc) (ctx : HttpContext) = task {
    ctx.Response.Cookies.Delete(".AspNetCore.Cookies")
    do! ctx.SignOutAsync()
    return! next ctx
}

let noAuthenticationRequired (nxt : HttpFunc) (ctx : HttpContext) = task { return! nxt ctx }

let requireLoggedIn : HttpFunc -> HttpContext -> HttpFuncResult =
    if Environment.IsDevelopmentEnvironment then
        noAuthenticationRequired
    else
        requiresAuthentication (RequestErrors.UNAUTHORIZED authScheme "Compositional IT" "You must be logged in.")

let authChallenge : HttpFunc -> HttpContext -> HttpFuncResult =
    if Environment.IsDevelopmentEnvironment then
        noAuthenticationRequired
    else
        requiresAuthentication (Auth.challenge authScheme)

let apiRouter = router {
    pipe_through requireLoggedIn
    pipe_through webApp
}

let appRouter = router {
    pipe_through authChallenge
    get "" (htmlFile "public/app.html")
}

let routes = router {
    not_found_handler (RequestErrors.notFound (text "Not Found"))
    forward "/api" apiRouter
    forward "/" appRouter
    get "/logout" logout
}

let configureHost (hostBuilder : IHostBuilder) =
    hostBuilder

let configureApp (app:IApplicationBuilder) =
    app
        .UseHsts() // See https://docs.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-3.1&tabs=visual-studio
        .UseHttpsRedirection() // As above
        .UseAuthentication()

let configureServices (services : IServiceCollection) =
    let config = services.BuildServiceProvider().GetService<IConfiguration>()
    services
        .AddMicrosoftIdentityWebAppAuthentication(config, openIdConnectScheme = authScheme)
        |> ignore

    services

let app =
    application {
        url "http://0.0.0.0:8085"
        use_developer_exceptions
        host_config configureHost
        service_config configureServices
        app_config configureApp
        use_router routes
        memory_cache
        use_static "public"
        use_gzip
    }

run app
