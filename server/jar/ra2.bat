@echo off
chcp 65001 > nul
rem set JDK_HOME=C:\Users\Administrator\.jdks\corretto-11.0.28
set JDK_HOME=..\jdk11.0.29_7
set JAVA=%JDK_HOME%\bin\java.exe

echo Starting ra2 server...
echo Using JDK: %JDK_HOME%

%JAVA% -Dfile.encoding=UTF-8 -cp "ra2.jar;config/*;libs/*;" org.game.ra2.GameStartUp

if %ERRORLEVEL% NEQ 0 (
    echo Failed to start ra2 server.
    pause
)