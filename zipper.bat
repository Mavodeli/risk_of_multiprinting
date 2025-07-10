cd build
DEL "risk_of_multiprinting.zip"
cd ..
xcopy "manifest.json" "%CD%\risk_of_multiprinting\bin\Debug\netstandard2.1"
xcopy "icon.png" "%CD%\risk_of_multiprinting\bin\Debug\netstandard2.1"
xcopy "README.md" "%CD%\risk_of_multiprinting\bin\Debug\netstandard2.1"
xcopy "CHANGELOG.md" "%CD%\risk_of_multiprinting\bin\Debug\netstandard2.1"
7z a -y -tzip "./build/risk_of_multiprinting.zip" "./risk_of_multiprinting/bin/Debug/netstandard2.1/*" -mx9
echo done
exit