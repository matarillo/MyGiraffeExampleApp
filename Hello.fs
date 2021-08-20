module MySample.Hello

open Microsoft.AspNetCore.Http
open Giraffe

// ---------------------------------
// Models
// ---------------------------------

type Message =
    {
        Text : string
    }

module View =
    open Giraffe.ViewEngine

    let layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "giraffe" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/main.css" ]
            ]
            body [] content
        ]

    let partial () =
        h1 [] [ encodedText "giraffe" ]

    let hello (model : Message) =
        [
            partial()
            p [] [ encodedText model.Text ]
        ] |> layout

let helloHandler (name : string) =
    let greetings = sprintf "Hello %s, from Giraffe!" name
    let model     = { Message.Text = greetings }
    let view      = View.hello model
    htmlView view

let queryHandler (next : HttpFunc) (ctx : HttpContext) =
    let q = ctx.Request.QueryString
    let name =
        if q.HasValue
            then q.Value.Substring 1 |> System.Net.WebUtility.UrlDecode
            else "world"
    helloHandler name next ctx
