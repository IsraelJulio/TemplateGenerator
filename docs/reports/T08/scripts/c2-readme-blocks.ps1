Write-Output "PSVersion: $($PSVersionTable.PSVersion)"
Write-Output "--- bloco powershell: Autenticacao (register/login)"
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
'{"email":"pessoa@exemplo.com","password":"Senha!123"}' | curl.exe -i -X POST http://localhost:5100/auth/register -H "Content-Type: application/json" --data-binary '@-'
Write-Output ""
'{"email":"pessoa@exemplo.com","password":"Senha!123"}' | curl.exe -i -X POST http://localhost:5100/auth/login -H "Content-Type: application/json" --data-binary '@-' | Select-Object -First 1
Write-Output "--- bloco powershell: TOKEN"
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$TOKEN = ('{"email":"pessoa@exemplo.com","password":"Senha!123"}' | curl.exe -s -X POST http://localhost:5100/auth/login -H "Content-Type: application/json" --data-binary '@-' | ConvertFrom-Json).accessToken
Write-Output "TOKEN length=$($TOKEN.Length)"
Write-Output "--- bloco powershell: CRUD"
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)
'{"title":"Primeiro item"}' | curl.exe -i -X POST http://localhost:5100/items -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" --data-binary '@-'
Write-Output ""
curl.exe http://localhost:5100/items -H "Authorization: Bearer $TOKEN"
Write-Output ""
Write-Output "exit(last curl)=$LASTEXITCODE"
