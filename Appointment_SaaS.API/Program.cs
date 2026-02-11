using System;
using Appointment_SaaS.Business.Abstract;
using Appointment_SaaS.Business.Concrete;
using Appointment_SaaS.Data.Abstract;
using Appointment_SaaS.Data.Concrete;
using Appointment_SaaS.Data.Context;
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 1. Connection String'i oku
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. DbContext'i projeye tanýt (Servis olarak ekle)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// --- DATA ACCESS (VERÝ) KATMANI KAYITLARI ---
// Interface ile Concrete sýnýflarý burada eþleþtiriyoruz
builder.Services.AddScoped<ITenantRepository, EfTenantRepository>();
builder.Services.AddScoped<IAppointmentRepository, EfAppointmentRepository>();
builder.Services.AddScoped<ISectorRepository, EfSectorRepository>();
builder.Services.AddScoped<IServiceRepository, EfServiceRepository>();

// --- BUSINESS (ÝÞ) KATMANI KAYITLARI ---
// Controller'lar bu interface'leri çaðýrdýðýnda hangi Manager'ýn çalýþacaðýný söylüyoruz
builder.Services.AddScoped<ITenantService, TenantManager>();
builder.Services.AddScoped<IAppointmentService, AppointmentManager>();
builder.Services.AddScoped<ISectorService, SectorManager>();
builder.Services.AddScoped<IServiceService, ServiceManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
