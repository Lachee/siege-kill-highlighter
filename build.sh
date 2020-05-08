#!/bin/bash
dotnet build
cd SiegeClipHighlighter.TesseractTool
./build.sh
cd ..
dotnet run --project SiegeClipHighlighter
