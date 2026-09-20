# Build em imagem separada da execução: o SDK tem ~800 MB e não precisa ir
# para produção — a imagem final leva só o runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /origem

# csproj primeiro, sozinho: enquanto as dependências não mudarem, o Docker
# reaproveita a camada do restore e não baixa pacote a cada alteração de código
COPY SchoolApi.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl entra só para o healthcheck: a imagem do runtime não traz cliente HTTP,
# e sem checagem de saúde quem depende da API sobe antes de ela responder
RUN apt-get update     && apt-get install --no-install-recommends -y curl     && rm -rf /var/lib/apt/lists/*

# usuário sem privilégio: container que roda como root é achado fácil em
# qualquer revisão de segurança
USER $APP_UID

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SchoolApi.dll"]
