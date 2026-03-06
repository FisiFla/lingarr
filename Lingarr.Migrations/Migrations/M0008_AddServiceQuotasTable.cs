using FluentMigrator;

namespace Lingarr.Migrations.Migrations;

[Migration(8)]
public class M0008_AddServiceQuotasTable : Migration
{
    public override void Up()
    {
        Create.Table("service_quotas")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("service_type").AsCustom("VARCHAR(50)").NotNullable().Unique()
            .WithColumn("monthly_limit_chars").AsInt64().Nullable()
            .WithColumn("chars_used").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("reset_month").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down()
    {
        Delete.Table("service_quotas");
    }
}
