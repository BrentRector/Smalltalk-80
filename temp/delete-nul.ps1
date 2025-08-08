# PowerShell script to delete the undeletable 'nul' file
# The \\?\ prefix tells Windows to bypass normal path processing

$nullFile = "\\?\D:\Users\brent\OneDrive\Src\Smalltalk-80\nul"

if (Test-Path -LiteralPath $nullFile) {
    Remove-Item -LiteralPath $nullFile -Force
    Write-Host "Successfully deleted the 'nul' file"
} else {
    Write-Host "The 'nul' file was not found"
}