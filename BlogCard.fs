module MySample.BlogCard

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Http

// ---------------------------------
// Models
// ---------------------------------

type Metadata =
    { Title: string;
      Url: string;
      Image: string;
      Description: string; }

let private svgIcon = "data:image/svg+xml;charset=utf8,%3Csvg xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22 viewBox%3D%220 0 16 16%22%3E%3Cpath d%3D%22M 8 2 C 6.169 2 4.5757656 3.2743906 4.1347656 5.0253906 C 3.0557656 5.1843906 2.1982031 6.0411563 2.0332031 7.1601562 C 0.83420313 7.5671563 4.6351811e-17 8.706 0 10 C 0 11.654 1.346 13 3 13 L 13 13 C 14.654 13 16 11.654 16 10 C 16 9.101 15.600062 8.2633125 14.914062 7.6953125 C 14.971063 7.4643125 15 7.231 15 7 C 15 5.18 13.389188 3.6958281 11.492188 4.0488281 C 10.790188 2.7918281 9.463 2 8 2 z M 8 3 C 9.208 3 10.292672 3.7180781 10.763672 4.8300781 L 10.943359 5.2558594 L 11.380859 5.109375 C 11.606859 5.035375 11.809 5 12 5 C 13.103 5 14 5.897 14 7 C 14 7.233 13.953328 7.4739375 13.861328 7.7109375 L 13.716797 8.09375 L 14.0625 8.3125 C 14.6495 8.6845 15 9.314 15 10 C 15 11.103 14.103 12 13 12 L 3 12 C 1.897 12 1 11.103 1 10 C 1 9.051 1.6745156 8.2260625 2.6035156 8.0390625 L 3.0117188 7.921875 L 3 7.5 C 3 6.673 3.6732188 5.9990938 4.4492188 5.9960938 L 4.5507812 6.0058594 L 4.9824219 6.0058594 L 5.0449219 5.5742188 C 5.2539219 4.1062187 6.525 3 8 3 z M 6.7519531 5.9433594 L 6.0449219 6.6503906 L 7.7929688 8.3984375 L 6.0449219 10.146484 L 6.7519531 10.853516 L 8.5 9.1054688 L 10.248047 10.853516 L 10.955078 10.146484 L 9.2070312 8.3984375 L 10.955078 6.6503906 L 10.248047 5.9433594 L 8.5 7.6914062 L 6.7519531 5.9433594 z%22%2F%3E%3C%2Fsvg%3E"

let private empty = {
    Title = "no url";
    Url = "";
    Image = svgIcon;
    Description = "";
}

let private couldNotConnectTo url = {
    Title = url;
    Url = url;
    Image = svgIcon;
    Description = "couldn't connect to host";
}

let private schemaNotAllowed url = {
    Title = url;
    Url = url;
    Image = svgIcon;
    Description = "Only 'http' and 'https' schemes are allowed";
}

module View =
    open Giraffe.ViewEngine

    let private layout (content: XmlNode list) =
        html [] [
            head [] [
                title []  [ encodedText "blog card" ]
                link [ _rel  "stylesheet"
                       _type "text/css"
                       _href "/card.css" ]
            ]
            body [] content
        ]

    let blogCard (model : Metadata) =
        [
            a [ _class "custom-snippet-link"
                _href model.Url ] [
                div [ _class "custom-snippet-eyecatch" ] [
                    img [ _src model.Image
                          _alt model.Title ]
                ]
                div [ _class "custom-snippet-information" ] [
                    h4 [ _class "custom-snippet-heading" ] [ encodedText model.Title ]
                    p [ _class "custom-snippet-description" ] [ encodedText model.Description ]
                ]
            ]
        ] |> layout

module External =
    open AngleSharp.Dom
    open System

    let private client =
        let client = new System.Net.Http.HttpClient()
        client.DefaultRequestHeaders.Add("User-Agent", "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)")
        client

    let private fromOpenData (doc: AngleSharp.Html.Dom.IHtmlDocument) () =
        let getOpenGraphData property (nodes: IHtmlCollection<IElement>)=
            nodes
            |> Seq.tryFind (fun e -> e.GetAttribute("property") = property)
            |> Option.map (fun e -> e.GetAttribute("content"))
        let meta = doc.Head.QuerySelectorAll("meta[property^='og:'][content]")
        let ogTitle = getOpenGraphData "og:title" meta
        let ogImage = getOpenGraphData "og:image" meta
        let ogUrl = getOpenGraphData "og:url" meta
        let ogDesc = getOpenGraphData "og:description" meta

        (ogTitle, ogImage, ogUrl) |> function
        | Some t, Some i, Some u -> Some { Title = t; Url = u; Image = i; Description = ogDesc |> Option.defaultValue "" }
        | _                      -> None

    let private defaultLocationOfFavicon (uri: Uri) =
        let favicon = Uri(uri, "/favicon.ico")
        favicon.ToString()

    let private fromHtml (url: string) (doc: AngleSharp.Html.Dom.IHtmlDocument) () =
        let absolutePath (uri: Uri) (url: string) =
            let abs = Uri(uri, url)
            abs.ToString()
        let favicon =
            let uri = Uri(url)
            doc.Head.QuerySelectorAll("link[rel*=icon][href]")
            |> Seq.tryHead
            |> Option.map (fun e -> e.GetAttribute("href"))
            |> Option.map (absolutePath uri)
            |> Option.defaultValue (defaultLocationOfFavicon uri)
        let title =
            doc.Head.QuerySelectorAll("title")
            |> Seq.tryHead
            |> Option.map (fun e -> e.TextContent)
            |> Option.defaultValue url

        { Title = url; Url = url; Image = favicon; Description = title }

    let download (url:string) =
        let isContentHtml (message: System.Net.Http.HttpResponseMessage) =
            let contentType = message.Content.Headers.ContentType
            match contentType with
            | null -> false
            | _    -> match contentType.MediaType with
                      | "text/html" -> true
                      | _           -> false

        task {
            try
                let! message = client.GetAsync(url)
                if isContentHtml message then
                    let! stream = message.Content.ReadAsStreamAsync()
                    let doc = (AngleSharp.Html.Parser.HtmlParser()).ParseDocument(stream)
                    if message.IsSuccessStatusCode then
                        return
                            (fromOpenData doc ())
                            |> Option.defaultWith (fromHtml url doc)
                    else
                        return
                            fromHtml url doc ()
                else
                    let title = if message.IsSuccessStatusCode then url else message.ReasonPhrase
                    let favicon = url |> Uri |> defaultLocationOfFavicon
                    return
                        { Title = url; Url = url; Image = favicon; Description = title; }
            with
                | :? System.Net.Http.HttpRequestException -> return couldNotConnectTo url
                | :? System.ArgumentException -> return schemaNotAllowed url
        }

let blogCardHandler (next : HttpFunc) (ctx : HttpContext) =
    let q = ctx.Request.QueryString
    let url =
        if q.HasValue then Some q.Value else None
        |> Option.map (fun x -> x.Substring 1)
        |> Option.map System.Net.WebUtility.UrlDecode
    task {
        let! model =
            match url with
            | Some url -> External.download url
            | None     -> task { return empty }
        let view = View.blogCard model
        return! htmlView view next ctx
    }
