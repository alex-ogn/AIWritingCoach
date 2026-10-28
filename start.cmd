@echo off
rem Starts the app with hot reload; dotnet watch opens the browser at http://localhost:5155 when it is ready.
dotnet watch --project "%~dp0src\AIWritingCoach.Web" --launch-profile http
