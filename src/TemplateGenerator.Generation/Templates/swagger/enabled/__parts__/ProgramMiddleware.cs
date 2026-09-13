
// Documento e interface apenas em Development (ADR-0001, RF-20). Em qualquer outro ambiente nem
// `/openapi/v1.json` nem `/swagger` respondem.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "__ProjectName__ v1"));
}
