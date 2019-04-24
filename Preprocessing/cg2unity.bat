@echo off
set input=0
set lod=0
call :args %*
if -%input%-==-0- goto usage
if -%lod%-==-0- goto usage

for %%i in (%input%\*) do java -jar %~dp0\c2x.jar -f COLLADA -i %%i -t MULTI_SURFACE -l %lod% -o %%~di%%~pi%%~ni.dae

goto exit
:args
if -%1%-==-- goto endargs
if -%1%-==---input- (
    if not exist %2% (
        echo Exit: Input file does not exist
    )
    set input=%2
) else if -%1%-==---lod- (
    if -%2%-==-LOD1- (
        set lod=%2
    ) else (
        if -%2%-==-LOD2- (
            set lod=%2
        ) else (
            echo Exit: Unknown level of detail
        )
    )
)
shift
shift
goto args
:endargs
exit /b
:usage
echo Usage: cg2unity --input folder --lod [LOD1^|LOD2]
:exit