# WolfboyDispatcher

ver 1.0.0

![wbd](./doc/wbd.png)

dataType : baseType というデータ型があるときに、upstreamからdownstreamへと、
**受取手指定をしつつ、データの型特定をせずに、**
データを流すことができる、ディスパッチャ。

## From upstream
data -> Dispatcher -> SendTo\<DOWNSTREAM_TARGET_TYPE\>(data)

    アップストリームからはディスパッチャ経由で特定のクラスに対してデータを流す
    (特にデータ型を指定せず流せる)

## To downstream(DOWNSTREAM_TARGET_TYPE)
Dispatcher <- SetReceiver(ReceiverMethod\<dataType\>)

    ダウンストリームは自身がもっているメソッドを
    「特定のdataTypeを受け取るハンドラ」として登録する

こうすることで、上流からはデータを受け取って欲しいクラスだけを指定してよく分からないデータを送付し、

下流ではセットしたReceiverが受け取れる型のデータだけが届く、というメッセージングの仕組み。

また、型指定によって下流もすぐさま別の下流への上流として振る舞うことができる。


## Multi Level Dispatch
複数のDispatcherを**適当な型単位**で定義することができるので、
階層構造を持たせてメッセージ伝搬をさせることができる。

次のコードでは、とある**SourceEmitter**型をキーとしたディスパッチ経路を指定して、WantToReceiveMessage1型のレシーバに向けてのみデータ(byte[])を流す。

```C#
Dispatchers<MessageBase>.DispatchRoute<SourceEmitter>().SendTo<WantToReceiveMessage1>(data);
```

SourceEmitter型以外の型でDispatchRoute<T>をセットすれば、別の経路として扱うことができる。

これにより、複数の階層をもったオブジェクト構造に対してデータを送付することが可能。


## Relay data between Receiver
次のようなコードで、**WantToReceiveMessage1**クラスの**ReceiveMessage2**メソッドで受け取った**Message2**型のデータを、**WantToReceiveMessage1**よりさらに下の**WantToReceiveMessage2**型のレシーバへと、そのまま受けわたすことができる。

```C#
public void ReceiveMessage2 (Message2 data) {
	Debug.LogError("ReceiveMessage2 received data:" + data.param2);

	// relay data to next downstream.
	Dispatchers<MessageBase>.DispatchRoute<WantToReceiveMessage1>().Relay<WantToReceiveMessage2>(data);
}
```

Relayされたデータを受け取る**WantToReceiveMessage2**型のコードでは、
**WantToReceiveMessage1**というDispatchRouteから、Message2型のデータを受け取るようになっている。

```C#
public class WantToReceiveMessage2 {
	public WantToReceiveMessage2 () {
		Dispatchers<MessageBase>.DispatchRoute<WantToReceiveMessage1>().SetReceiver<Message2>(Receiver);
	}

	public void Receiver (Message2 data) {
		Debug.LogError("WantToReceiveMessage2 received data:" + data.param2);
	}
}
```


## Multi DataType Dispatch
次のコードでは、とある**MessageBase**型を基底としたデータを扱うディスパッチャを扱っている。

```C#
Dispatchers<MessageBase>.DispatchRoute<SourceEmitter>().SendTo<WantToReceiveMessage1>(data);
```

Dispatcher<T>に別の基底型をセットすれば、別のデータを運ぶディスパッチャとして使える。
