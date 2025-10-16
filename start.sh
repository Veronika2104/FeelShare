#!/bin/sh
export ASPNETCORE_URLS=http://0.0.0.0:${PORT}
exec dotnet FeelShare.dll
