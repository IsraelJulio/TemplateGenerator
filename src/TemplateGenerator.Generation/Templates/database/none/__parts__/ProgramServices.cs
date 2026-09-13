
// Armazenamento em memória (RF-16): uma instância para todo o processo, perdida no reinício.
builder.Services.AddSingleton<IItemStore, ItemStore>();
