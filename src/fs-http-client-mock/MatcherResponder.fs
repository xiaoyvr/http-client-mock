﻿namespace HttpClientMock

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Runtime.InteropServices
open JetBrains.Annotations


module MatcherResponder =
    let MatchUrl<'TR> (urlMatcher: string -> bool) (request: HttpRequestMessage)  =
        let p = request.RequestUri.PathAndQuery
        if (p |> urlMatcher) || (p |> Uri.UnescapeDataString |> urlMatcher) then
            Some request
        else
            None
            
    let MatchMethod<'TR> (method: HttpMethod) (request: HttpRequestMessage)  =
        if request.Method <> method then None else Some(request)
        
    let MatchRequest<'TR> (matchFunc: RequestCapture -> 'TR option -> bool) (request: HttpRequestMessage)  =
        let capture = RequestCapture(request.RequestUri, request.Method, Option.ofObj request.Content)
        let model = capture.Model<'TR>()
        match matchFunc capture model with
            | true -> Some(request)
            | false -> None
    
type MatcherResponder internal (matcher: HttpRequestMessage -> HttpRequestMessage option) =
    let capturer = RequestCapturer()
    let mutable matcher = matcher
    let responder = Responder(capturer)
    
    member this.MatchRequest<'TR>(matchFunc: Func<RequestCapture, 'TR, bool>) =
        let fsMatchFunc = fun requestCapture model ->
            match model with
                | Some m -> matchFunc.Invoke(requestCapture, m)
                | None -> matchFunc.Invoke(requestCapture, Unchecked.defaultof<'TR> )

        matcher <- matcher >> Option.bind (MatcherResponder.MatchRequest<'TR> fsMatchFunc)
        responder

    /// this is only for csharp use since there's no type declaration for anonymous type
    [<PublicAPI>]
    member this.MatchRequest<'TR>(matchFunc: Func<RequestCapture, 'TR, bool>, _: 'TR) =        
        this.MatchRequest<'TR>(matchFunc)
            
    [<PublicAPI>]
    member this.Respond(status: HttpStatusCode,
                        contentFn: Func<HttpRequestMessage, HttpContent>,
                        [<Optional>]?headers: struct(string*string) seq,
                        [<Optional>]?location: Uri) =        
        responder.Respond(status, contentFn, ?headers = headers, ?location = location)
        
    [<PublicAPI>]
    member this.Respond(status: HttpStatusCode,
                        [<Optional>]?content: Object,
                        [<Optional>]?headers: struct(string*string) seq,
                        [<Optional>]?location: Uri) =
        responder.Respond(status, ?content = content, ?headers = headers, ?location = location)
        
    member internal this.Build() : Behavior =
        {
            Match = matcher
            Respond = responder.Respond
            Capture = capturer.Intake
        }

    