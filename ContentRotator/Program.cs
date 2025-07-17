// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
var controlPipe = new NamedPipeClient("mpv_control_pipe");
var contentFolderPath = @"c:\data\VideoKiosk";

var commandText = "playlist-clear";
Console.WriteLine(commandText);
controlPipe.SendCommand(commandText);

commandText = @$"loadlist {contentFolderPath}\02.txt insert-next";
Console.WriteLine(commandText);
controlPipe.SendCommand(commandText);

