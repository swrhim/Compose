#r "bin\Debug\Newtonsoft.Json.dll"
#r "bin\Debug\FSharp.Data.dll"
#r "bin\Debug\FSharp.Core.dll"
#load "WebSocket.fs"

open System
open System.Net
open FSharp.Data
open FSharp.Core
open Newtonsoft


type GatewayResponse = {
    heartBeatInterval: int
    trace : string list
}

type WebSocketUrl = {
  url : string
}

let start = async{ 
  let! request = Http.AsyncRequestString("https://discordapp.com/api/gateway")
  let webSocketUrl = JsonConverter.DeserializeObject<WebSocketUrl> request
  let ws = WebSocket.create() 
  WebSocket.read ws webSocketUrl.url
} 

start 
|> Async.RunSynchronously