@echo off
call "C:\Program Files (x86)\Mono\\bin\setmonopath.bat"
mcs -out:SocksServer.exe Program.cs ActiveClient.cs Helpers.cs MSHelpers.cs BufferManager.cs Server.cs