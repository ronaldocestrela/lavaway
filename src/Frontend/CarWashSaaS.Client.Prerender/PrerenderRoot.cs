using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using CarWashSaaS.Client.Web.Layout;

namespace CarWashSaaS.Client.Prerender;

/// <summary>
/// Root host component used during build-time static prerendering to wrap public Razor pages
/// with authentication state and layout without booting the WebAssembly runtime.
/// </summary>
public sealed class PrerenderRoot : ComponentBase
{
    [Parameter] public Type ComponentType { get; set; } = default!;
    [Parameter] public Type LayoutType { get; set; } = typeof(LandingLayout);
    [Parameter] public IDictionary<string, object?>? Parameters { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingAuthenticationState>(0);
        builder.AddAttribute(1, nameof(CascadingAuthenticationState.ChildContent), (RenderFragment)(b =>
        {
            b.OpenComponent<LayoutView>(2);
            b.AddAttribute(3, nameof(LayoutView.Layout), LayoutType);
            b.AddAttribute(4, nameof(LayoutView.ChildContent), (RenderFragment)(inner =>
            {
                inner.OpenComponent(5, ComponentType);
                if (Parameters is not null)
                {
                    var seq = 6;
                    foreach (var kv in Parameters)
                    {
                        inner.AddAttribute(seq++, kv.Key, kv.Value);
                    }
                }
                inner.CloseComponent();
            }));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    }
}
