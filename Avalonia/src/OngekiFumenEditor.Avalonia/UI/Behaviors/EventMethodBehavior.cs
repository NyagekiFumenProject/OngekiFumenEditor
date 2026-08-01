using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Xaml.Interactivity;
using OngekiFumenEditor.Avalonia.Compat;

namespace OngekiFumenEditor.Avalonia.UI.Behaviors;

/// <summary>
/// 调用 ViewModel 方法时传入的参数模式，对应 Caliburn.Micro cal:Message.Attach 的 $ 参数。
/// </summary>
public enum EventMethodPassMode
{
    /// <summary>无参数，对应 [Action Foo()]。</summary>
    None,

    /// <summary>传入 Compat.ActionExecutionContext，对应 $executionContext。</summary>
    ExecutionContext,

    /// <summary>传入触发元素自身的 DataContext（如行/单元格项），对应 $dataContext。</summary>
    DataContext,

    /// <summary>传入触发事件的控件，对应 $source。</summary>
    Source,

    /// <summary>传入原始事件参数，对应 $eventArgs。</summary>
    EventArgs,

    /// <summary>传入逻辑树根部的视图，对应 $view。</summary>
    View
}

/// <summary>
/// 替代 Caliburn.Micro cal:Message.Attach 的轻量行为：指定事件触发时调用
/// DataContext（ViewModel）上的方法，并按 PassMode 传参。
/// 事件名使用 Avalonia 命名（如 PointerPressed / DoubleTapped / PointerWheelChanged）。
/// </summary>
public class EventMethodBehavior : Behavior<Control>
{
    public static readonly StyledProperty<string> EventNameProperty =
        AvaloniaProperty.Register<EventMethodBehavior, string>(nameof(EventName));

    public static readonly StyledProperty<string> MethodNameProperty =
        AvaloniaProperty.Register<EventMethodBehavior, string>(nameof(MethodName));

    public static readonly StyledProperty<EventMethodPassMode> PassModeProperty =
        AvaloniaProperty.Register<EventMethodBehavior, EventMethodPassMode>(nameof(PassMode));

    public static readonly StyledProperty<string> GestureProperty =
        AvaloniaProperty.Register<EventMethodBehavior, string>(nameof(Gesture));

    private static readonly ConcurrentDictionary<(Type TargetType, string MethodName), MethodInfo> methodCache = new();

    private EventInfo subscribedEvent;
    private Delegate subscribedDelegate;
    private RoutedEvent subscribedRoutedEvent;
    private Delegate subscribedRoutedDelegate;
    private IDisposable propertySubscription;

    public string EventName
    {
        get => GetValue(EventNameProperty);
        set => SetValue(EventNameProperty, value);
    }

    public string MethodName
    {
        get => GetValue(MethodNameProperty);
        set => SetValue(MethodNameProperty, value);
    }

    public EventMethodPassMode PassMode
    {
        get => GetValue(PassModeProperty);
        set => SetValue(PassModeProperty, value);
    }

    /// <summary>
    /// EventName 为 "KeyGesture" 时使用的按键手势（如 "Delete"、"Ctrl+S"），
    /// 对应 Caliburn 的 [Key Delete] 语法。
    /// </summary>
    public string Gesture
    {
        get => GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        Subscribe();
    }

    protected override void OnDetachedFromVisualTree()
    {
        Unsubscribe();
        base.OnDetachedFromVisualTree();
    }

    private void Subscribe()
    {
        if (AssociatedObject is null || string.IsNullOrEmpty(EventName) || string.IsNullOrEmpty(MethodName))
            return;

        // [Key Xxx] 语法：挂 KeyDown，触发时按 Gesture 过滤。
        if (EventName == "KeyGesture")
        {
            KeyGesture parsedGesture = null;
            if (!string.IsNullOrEmpty(Gesture))
            {
                try
                {
                    parsedGesture = KeyGesture.Parse(Gesture);
                }
                catch (Exception)
                {
                    Debug.WriteLine($"[EventMethodBehavior] 无法解析按键手势 {Gesture}");
                }
            }
            var handler = new EventHandler<KeyEventArgs>((s, e) =>
            {
                if (parsedGesture is null || parsedGesture.Matches(e))
                    Invoke(s, e);
            });
            AssociatedObject.AddHandler(InputElement.KeyDownEvent, handler,
                RoutingStrategies.Bubble | RoutingStrategies.Tunnel);
            subscribedRoutedEvent = InputElement.KeyDownEvent;
            subscribedRoutedDelegate = handler;
            return;
        }

        // DragDrop 的拖放事件是附加事件，直接 AddHandler。
        var dragEvent = EventName switch
        {
            "DragEnter" => DragDrop.DragEnterEvent,
            "DragOver" => DragDrop.DragOverEvent,
            "DragLeave" => DragDrop.DragLeaveEvent,
            "Drop" => DragDrop.DropEvent,
            _ => null
        };
        if (dragEvent is not null)
        {
            // DragDrop 事件的处理器签名是 EventHandler<DragEventArgs>。
            subscribedRoutedDelegate = new EventHandler<DragEventArgs>((s, e) => Invoke(s, e));
            AssociatedObject.AddHandler(dragEvent, subscribedRoutedDelegate);
            subscribedRoutedEvent = dragEvent;
            return;
        }

        var eventInfo = AssociatedObject.GetType().GetEvent(EventName,
            BindingFlags.Public | BindingFlags.Instance);
        if (eventInfo is not null)
        {
            var handlerMethod = eventInfo.EventHandlerType.GetMethod("Invoke");
            var parameters = handlerMethod.GetParameters();
            var argsType = parameters.Length > 1 ? parameters[1].ParameterType : typeof(EventArgs);
            var core = GetType().GetMethod(nameof(OnEventCore), BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(argsType);
            subscribedDelegate = Delegate.CreateDelegate(eventInfo.EventHandlerType, this, core);
            eventInfo.AddEventHandler(AssociatedObject, subscribedDelegate);
            subscribedEvent = eventInfo;
            return;
        }

        // WPF 的 XxxChanged（如 FocusableChanged）没有等价事件，退化为属性观察。
        if (EventName.EndsWith("Changed", StringComparison.Ordinal))
        {
            var propName = EventName[..^"Changed".Length];
            var prop = AvaloniaPropertyRegistry.Instance
                .FindRegistered(AssociatedObject, propName);
            if (prop is not null)
                propertySubscription = AssociatedObject
                    .GetObservable(prop)
                    .Subscribe(v => Invoke(AssociatedObject, v));
            return;
        }

        Debug.WriteLine($"[EventMethodBehavior] 在 {AssociatedObject.GetType().Name} 上找不到事件 {EventName}");
    }

    private void Unsubscribe()
    {
        if (subscribedEvent is not null && subscribedDelegate is not null && AssociatedObject is not null)
            subscribedEvent.RemoveEventHandler(AssociatedObject, subscribedDelegate);
        subscribedEvent = null;
        subscribedDelegate = null;

        if (subscribedRoutedEvent is not null && subscribedRoutedDelegate is not null && AssociatedObject is not null)
            AssociatedObject.RemoveHandler(subscribedRoutedEvent, subscribedRoutedDelegate);
        subscribedRoutedEvent = null;
        subscribedRoutedDelegate = null;

        propertySubscription?.Dispose();
        propertySubscription = null;
    }

    // 通过反射挂到任意 EventHandler<TArgs> 形态的事件上。
    private void OnEventCore<TArgs>(object sender, TArgs args) => Invoke(sender, args);

    private void Invoke(object sender, object args)
    {
        var target = AssociatedObject?.DataContext;
        if (target is null)
            return;

        var methodCacheKey = (target.GetType(), MethodName);
        if (!methodCache.TryGetValue(methodCacheKey, out var method))
        {
            method = methodCacheKey.Item1
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == methodCacheKey.MethodName);
            if (method is not null)
                methodCache.TryAdd(methodCacheKey, method);
        }

        if (method is null)
        {
            Debug.WriteLine($"[EventMethodBehavior] 在 {target.GetType().Name} 上找不到方法 {MethodName}");
            return;
        }

        try
        {
            if (method.GetParameters().Length == 0)
            {
                method.Invoke(target, null);
                return;
            }

            var param = PassMode switch
            {
                EventMethodPassMode.ExecutionContext => new ActionExecutionContext
                {
                    Source = sender ?? AssociatedObject,
                    EventArgs = args,
                    View = FindView()
                },
                EventMethodPassMode.DataContext => (sender as Control ?? AssociatedObject)?.DataContext,
                EventMethodPassMode.Source => sender ?? AssociatedObject,
                EventMethodPassMode.EventArgs => args,
                EventMethodPassMode.View => FindView(),
                _ => null
            };

            var paramType = method.GetParameters()[0].ParameterType;
            if (param is not null && !paramType.IsInstanceOfType(param))
            {
                Debug.WriteLine(
                    $"[EventMethodBehavior] {MethodName} 参数类型不匹配：需要 {paramType.Name}，实际 {param.GetType().Name}");
                return;
            }

            method.Invoke(target, new[] { param });
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[EventMethodBehavior] 调用 {MethodName} 失败：{e}");
        }
    }

    private object FindView() =>
        AssociatedObject?.GetLogicalAncestors().LastOrDefault() as Control
        ?? (object)AssociatedObject;
}
