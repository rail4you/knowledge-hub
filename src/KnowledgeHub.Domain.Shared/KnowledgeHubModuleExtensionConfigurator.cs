using System;
using System.ComponentModel.DataAnnotations;
using KnowledgeHub.Users;
using Volo.Abp.Identity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Threading;

namespace KnowledgeHub;

public static class KnowledgeHubModuleExtensionConfigurator
{
    private static readonly OneTimeRunner OneTimeRunner = new OneTimeRunner();

    public static void Configure()
    {
        OneTimeRunner.Run(() =>
        {
            ConfigureExistingProperties();
            ConfigureExtraProperties();
        });
    }

    private static void ConfigureExistingProperties()
    {
        /* You can change max lengths for properties of the
         * entities defined in the modules used by your application.
         *
         * Example: Change user and role name max lengths

           AbpUserConsts.MaxNameLength = 99;
           IdentityRoleConsts.MaxNameLength = 99;

         * Notice: It is not suggested to change property lengths
         * unless you really need it. Go with the standard values wherever possible.
         *
         * If you are using EF Core, you will need to run the add-migration command after your changes.
         */
    }

    private static void ConfigureExtraProperties()
    {
        ObjectExtensionManager.Instance.Modules()
           .ConfigureIdentity(identity =>
           {
               identity.ConfigureUser(user =>
               {
                   user.AddOrUpdateProperty<UserRoleType>(
                       "RoleType",
                       property =>
                       {
                       }
                   );

                    user.AddOrUpdateProperty<string>(
                        "SchoolId",
                        property =>
                        {
                            property.Attributes.Add(new StringLengthAttribute(50));
                        }
                    );

                   user.AddOrUpdateProperty<string>(
                       "EmployeeNumber",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(50));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "Department",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(100));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "Major",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(100));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "Course",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(200));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "Title",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(50));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "StudentNumber",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(50));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "Grade",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(50));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "ClassName",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(50));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "ManagementScope",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(500));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "CompanyName",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(200));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "UnifiedSocialCreditCode",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(18));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "Position",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(50));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "Industry",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(100));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "PartnerSchool",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(500));
                       }
                   );

                   user.AddOrUpdateProperty<string>(
                       "Remark",
                       property =>
                       {
                           property.Attributes.Add(new StringLengthAttribute(500));
                       }
                   );
               });
           });
    }
}
