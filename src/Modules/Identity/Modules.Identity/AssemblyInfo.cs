using Trislab.Framework.Web.Modules;
using System.Runtime.CompilerServices;

[assembly: TrislabModule(typeof(Trislab.Modules.Identity.IdentityModule), 100)]
[assembly: InternalsVisibleTo("Identity.Tests")]
[assembly: InternalsVisibleTo("Integration.Tests")]