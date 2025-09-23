using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace WorkPartner
{
    public class AppDbContext : DbContext
    {
        // 테이블로 매핑될 DbSet 속성들을 정의합니다.
        public DbSet<TimeLogEntry> TimeLogs { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<TodoItem> Todos { get; set; }
        public DbSet<ShopItem> ShopItems { get; set; }
        public DbSet<AppSettings> AppSettings { get; set; }

        // 데이터베이스 파일의 경로를 지정합니다.
        private static readonly string DbPath = DataManager.AppDataFolder;

        // OnConfiguring 메서드는 데이터베이스 연결 설정을 담당합니다.
        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={System.IO.Path.Combine(DbPath, "workpartner.db")}");

        // 모델이 생성될 때 초기 데이터를 시드(Seed)합니다.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // AppSettings에 대한 기본값 설정
            modelBuilder.Entity<AppSettings>().HasData(
                new AppSettings
                {
                    Id = 1, // 기본 키 값 설정
                    Username = "사용자",
                    Coins = 100,
                    Theme = "Light",
                    AccentColor = "#007ACC",
                    IsIdleDetectionEnabled = true,
                    IdleTimeoutSeconds = 300,
                    IsMiniTimerEnabled = false,
                    MiniTimerShowInfo = true,
                    MiniTimerShowCharacter = true,
                    MiniTimerShowBackground = true,
                    FocusModeNagMessage = "집중 모드 중입니다!"
                }
            );

            // 필요한 경우 여기에 ShopItems의 기본 데이터를 추가할 수 있습니다.
            // 예: modelBuilder.Entity<ShopItem>().HasData(new ShopItem { ... });
        }
    }
}
