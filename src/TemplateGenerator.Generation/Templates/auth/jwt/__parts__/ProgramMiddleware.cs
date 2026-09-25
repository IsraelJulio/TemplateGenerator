
// Autenticação e autorização entram antes dos endpoints — é o que faz o grupo protegido enxergar
// o usuário do token. O ASP.NET Core acrescentaria as duas sozinho; elas estão escritas para que o
// pipeline do projeto seja legível inteiro, sem conhecimento implícito.
app.UseAuthentication();
app.UseAuthorization();
