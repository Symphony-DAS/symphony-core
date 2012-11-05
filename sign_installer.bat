REM Sign Symphony Installer
REM sign <folder>

'C:\Program Files (x86)\Microsoft SDKs\Windows\v7.0A\Bin\signtool.exe' sign /t http://timestamp.digicert.com /a %1\setup.exe
'C:\Program Files (x86)\Microsoft SDKs\Windows\v7.0A\Bin\signtool.exe'sign /t http://timestamp.digicert.com /a %1\Symphony.msi