using System.Reflection;
using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class NAudioWavePlayerFactoryContractTests
{
    [Fact]
    public void FactoryContract_CreateDefaultWavePlayer_HasExactTaskOfIWavePlayerSignature()
    {
        var contract = typeof(INAudioWavePlayerFactory);
        var methods = contract.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.True(contract.IsPublic);
        Assert.True(contract.IsInterface);
        Assert.Single(methods);

        var method = methods[0];
        Assert.Equal(nameof(INAudioWavePlayerFactory.CreateDefaultWavePlayer), method.Name);
        Assert.Equal(typeof(Task<IWavePlayer>), method.ReturnType);
        Assert.Empty(method.GetParameters());
        Assert.False(method.IsGenericMethod);
        Assert.False(method.IsStatic);
    }
}
