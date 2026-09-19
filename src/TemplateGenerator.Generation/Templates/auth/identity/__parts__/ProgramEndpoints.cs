
// Os endpoints nativos do Identity, agrupados em `/auth`: cadastro em `/auth/register`, login em
// `/auth/login`, renovação em `/auth/refresh` e a gestão da conta em `/auth/manage/...`. Nenhum
// deles é escrito à mão — quem os mapeia é o próprio ASP.NET Core.
app.MapGroup("/auth")
    .MapIdentityApi<AppUser>()
    .WithTags("Identity");
