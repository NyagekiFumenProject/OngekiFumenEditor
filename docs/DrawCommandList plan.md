1-1. 可能同时存在多个IRenderContext
1-2. 可以通过IRenderManagerImpl.CreateDrawCommandListBuilder()得到IDrawCommandListBuilder对象
1-3. 通过IDrawCommandListBuilder对象的一系列方法DrawXXXX(), 比如DrawTexture(), DrawLines, ApplyViewMatrix, ApplyProjectionMatrix之类, 这些方法会生成对应的DrawCommand
1-4. IDrawCommandListBuilder.GetDrawCommandList可以获取DrawCommandList对象
1-5. 可以通过IRenderManagerImpl.PostDrawCommandList(IRenderContext, GetDrawCommandList)来提交，后者内部会SetBackDrawCommandList(IRenderContext, GetDrawCommandList)
1-6. 通过合适的时机(比如IRenderContext.OnRender回调), 准备和判断条件后分别调用IRenderManagerImpl.SwapDrawCommandList(IRenderContext)和PresentDrawCommandList(IRenderContext)

## IDrawCommandListBuilder 内部构成, 都要构成对应的DrawCommand
2-1. 各种IDrawing的方法转换而来，比如ISimpleDrawing可以定义成DrawSimpleLines(IEnumerable<LineVertex> points, float lineWidth)之类
2-2. 设置MVP之类比如ApplyViewMatrix/ApplyProjectionMatrix/PushViewMatrix/PopViewMatrix之类

## PresentDrawCommandList大概流程
3-1. 遍历DrawCommandList， 向后检查是否能合批处理, 无法合批就开始按照各项DrawCommand去实现，目前暂时是直接转发给对应的IDrawing去处理

## 当前计划
实现以上内容，但不要当前接入编辑器/波形渲染循环， 目前只需要实现内容

## 后续规划(当前暂不实现但要考虑)
1. 新的渲染线程负责步骤1-6， IRenderContext.OnRender只需要拿到渲染好去更新画面即可
2. 实现离屏渲染
