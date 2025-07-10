cd build
DEL "risk_of_multiprinting.zip"
cd ..
7z a -y -tzip "./build/risk_of_multiprinting.zip" "manifest.json"
7z a -y -tzip "./build/risk_of_multiprinting.zip" "icon.png"
7z a -y -tzip "./build/risk_of_multiprinting.zip" "README.md"
7z a -y -tzip "./build/risk_of_multiprinting.zip" "CHANGELOG.md"
7z a -y -tzip "./build/risk_of_multiprinting.zip" "./risk_of_multiprinting/bin/Debug/netstandard2.1/*" -mx9
echo done
exit