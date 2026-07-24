param(
    [string]$version = "1.0.0"
)

# build and produce nupkg (will appear in ./nupkgs)
dotnet pack -c Release .\Lattice.Runtime\Lattice.Runtime.csproj
# remove previous global install if exists
dotnet tool uninstall --global lattice
# install from local folder (tool manifest not required for global install)
dotnet tool install --global --add-source ./Lattice.Runtime/bin/Release --version $version lattice