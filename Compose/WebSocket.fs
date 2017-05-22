module WebSocket

open System
open System.Text
open System.Net.WebSockets
open System.Threading
open FSharp.Data
open FSharp.Control

type WebSocketCommand = 
    | Close
    | Message of msg : string

let utf8 = new UTF8Encoding()

let rec private readMessage (ws:WebSocket) : Async<WebSocketCommand> =
    let buffer : byte array = Array.zeroCreate 1024
    async {
        let! payload = ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None) |> Async.AwaitTask
        match payload.MessageType with
            | WebSocketMessageType.Binary -> return failwith "unsupported binary message"
            | WebSocketMessageType.Close -> return WebSocketCommand.Close
            | WebSocketMessageType.Text -> 
                let content = utf8.GetString(buffer).TrimEnd([| Char.MinValue |]) // trim null terminated string end
                if payload.EndOfMessage then
                    return WebSocketCommand.Message(content)
                else
                    let! next = readMessage ws
                    return match next with
                            | WebSocketCommand.Message(nextmsg) -> WebSocketCommand.Message(content + nextmsg)
                            | other -> other
            | _ -> return failwith "unexpected enum value"
    }

let private closeWebsocket (ws : ClientWebSocket) =
    ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None)
        |> Async.AwaitIAsyncResult
        |> Async.Ignore

let create() = new ClientWebSocket()
        
let send (ws : ClientWebSocket) (command : WebSocketCommand) =
    async {
        match command with 
            | WebSocketCommand.Close -> do! closeWebsocket ws  // we request close
            | WebSocketCommand.Message(text) ->  // we send message to remote
                do! ws.SendAsync(new ArraySegment<byte>(utf8.GetBytes(text)), WebSocketMessageType.Text, true, CancellationToken.None)
                    |> Async.AwaitIAsyncResult
                    |> Async.Ignore
    }

let read (ws : ClientWebSocket) (url : string) : AsyncSeq<string> =
    asyncSeq {
        do! ws.ConnectAsync(new Uri(url), CancellationToken.None) |> Async.AwaitTask
        while ws.State = WebSocketState.Open do
            let! msg = readMessage ws
            match msg with
            | WebSocketCommand.Close -> do! closeWebsocket ws // remote requests close
            | WebSocketCommand.Message(msg) -> // remote sends message to us
                yield msg
    }