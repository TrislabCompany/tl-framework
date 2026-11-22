using System.Runtime.CompilerServices;
using Trislab.Framework.Web.Modules;

[assembly: TrislabModule(typeof(Trislab.Modules.Notifications.NotificationsModule), 750)]
[assembly: InternalsVisibleTo("Notifications.Tests")]
[assembly: InternalsVisibleTo("Integration.Tests")]
