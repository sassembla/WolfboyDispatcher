using System.Text;
using UnityEngine;
using WolfboyDispatcher;


/*
	階層構造をもっているような状態で、上位はディスパッチャ経由で特定のクラスへとデータを流す。
	下位はクラスとレシーバーをディスパッチャへと登録する。

	というようなことを、wolfboyDispatcherはなんというか雑に可能にする。

	同時にこのサンプルは、base型を持つデータを、一切登録せずに型指定とコンストラクタ指定のみで振り分ける、ということを達成している。
	いや〜〜記述が少ないのはいいねえ。
 */
public class SourceEmitter : MonoBehaviour {

	public enum BattleState {
		None,
		Running,
	}

	private BattleState state;

	// Use this for initialization
	void Start () {
		state = BattleState.Running;

		// 2つ同じ型のレシーバを作っても、一つの型に対して一個しかレシーバ関数を登録できないので、後勝ちになる = 一件しかレシーバが残らない。
		var s = new WantToReceiveMessage1();
		var t = new WantToReceiveMessage1();

		
		// ちょっと扱いが難しいが、observerのremoveもできる。
		// set nested receiver.
		var u = new WantToReceiveMessage2();
		// remove nested receiver.
		Dispatchers<MessageBase>.DispatchRoute<WantToReceiveMessage1>().RemoveReceiver<Message2, WantToReceiveMessage2>();


		// 擬似的なレシーブ動作
		var data = new Message1();
		data.param = "a";
		var jsonStr = JsonUtility.ToJson(data);
		var jsonBytes = Encoding.UTF8.GetBytes(jsonStr);

		var data2 = new Message2("fufufu");
		var jsonStr2 = JsonUtility.ToJson(data2);
		var jsonBytes2 = Encoding.UTF8.GetBytes(jsonStr2);

		var data3 = new Message3("hehehe");// このデータは受取手が存在しない
		var jsonStr3 = JsonUtility.ToJson(data3);
		var jsonBytes3 = Encoding.UTF8.GetBytes(jsonStr3);

		for (var i = 0; i < 10; i++) {
			var dataBox = jsonBytes;

			if (i % 3 == 1) {
				dataBox = jsonBytes2;
			}
			if (i % 3 == 2) {
				dataBox = jsonBytes3;
			}
			
			Input(dataBox);
		}
	}

	public void Input (byte[] data) {
		switch (state) {
			case BattleState.None: {
				// do nothing.
				break;
			}
			case BattleState.Running: {
				// この時にイベントを受け取らせたいクラスを指定し、受け取り用に登録されているメソッドを着火する。
				// 該当するデータ型のレシーバが登録されていなければ無視される。

				// パターンその1、データの型を完全に無視してbyte[]のまま特定の下流型に流す。
				// 下流では指定した型のデータのみを受け取ることができる。
				{
					Dispatchers<MessageBase>.DispatchRoute<SourceEmitter>().SendTo(data, typeof(WantToReceiveMessage1));
				}
				

				// パターンその２、ここでデシリアライズして、デシリアライズしたデータを特定の下流型に流す。
				// この書き方だと、ここで全てのデータのデシリアライズを記述しなければいけなくてダルい(避けたくて上のパターンがある)。
				{
					// determine type here.
					var deserializeType = TypeIdentificationResolver.DetermineMessageType(data);
					switch (deserializeType) {
						case MessageType._1: {
							var deserializedData1 = TypeIdentificationResolver.Deserialize<Message1>(data);
							Dispatchers<MessageBase>.DispatchRoute<SourceEmitter>().Relay(deserializedData1, typeof(WantToReceiveMessage1));
							break;
						}
						case MessageType._2: {
							var deserializedData2 = TypeIdentificationResolver.Deserialize<Message2>(data);
							Dispatchers<MessageBase>.DispatchRoute<SourceEmitter>().Relay(deserializedData2, typeof(WantToReceiveMessage1));
							break;
						}
					}
				}
				
				break;
			}
		}
	}
}

/**
	このクラスは、特定のメッセージだけを、特定の関数で受け取りたい。
 */
public class WantToReceiveMessage1 {
	public WantToReceiveMessage1 () {
		Dispatchers<MessageBase>.DispatchRoute<SourceEmitter>().SetReceiver<Message1>(ReceiveMessage1);
		Dispatchers<MessageBase>.DispatchRoute<SourceEmitter>().SetReceiver<Message2>(ReceiveMessage2);
	}

	public void ReceiveMessage1 (Message1 data) {
		Debug.Log("ReceiveMessage1 received data:" + data.param);
	}

	public void ReceiveMessage2 (Message2 data) {
		Debug.Log("ReceiveMessage2 received data:" + data.param2);

		// relay data to next downstream.
		Dispatchers<MessageBase>.DispatchRoute<WantToReceiveMessage1>().Relay(data, typeof(WantToReceiveMessage2));
	}
}

public class WantToReceiveMessage2 {
	public WantToReceiveMessage2 () {
		Dispatchers<MessageBase>.DispatchRoute<WantToReceiveMessage1>().SetReceiver<Message2>(Receiver);
	}

	public void Receiver (Message2 data) {
		Debug.Log("WantToReceiveMessage2 received data:" + data.param2);
	}
}


/**
	enum for data type determination.
 */
public enum MessageType {
	Base,
	_1,
	_2,
	_3
}


/**
	通信でデータが飛んでくる用の型定義
	その基礎となる型
 */
public class MessageBase {
	public MessageType type;
	public MessageBase (MessageType type) {
		this.type = type;
	}
}

public class Message1 : MessageBase {
	public string param;
	public Message1 () : base (MessageType._1) {

	}
}

public class Message2 : MessageBase {
	public string param2;

	// パラメータ付きコンストラクタを持つ場合は、デフォルトコンストラクタの定義も必要になってしまう。
	// 何らかの方法でこのデータ型と識別子をマッピングしなければいけない都合がある。例えここでデフォルトコンストラクタを作らないで済む実装が作れたとしても、
	// 識別子:この型、というデータを作る必要がある都合上、この型をnewできる引数ゼロのコンストラクが結局必要になる。
	public Message2 () : base (MessageType._2) {
	}
	public Message2 (string param2) : base (MessageType._2) {
		this.param2 = param2;
	}
}

// このデータは受取手が存在しない。
public class Message3 : MessageBase {
	public string param3;

	public Message3 () : base (MessageType._3) {
	}
	public Message3 (string param3) : base (MessageType._3) {
		this.param3 = param3;
	}
}


/*
	通信で流すデータとして、MessageBaseとそれを拡張した型があるとき、
	そのMessageBaseに何らかのパラメータを持たせ、拡張した型がなんなのかを見分けたい、みたいなのがあると思う。
	
	このクラスでは、その判別手段を提供する。
	本当はMessageBaseにこれらの機能を提供したいんだけど、サーバとかでも同じ型を分解したい = 型情報をClient - Serverで共有したい、というニーズがあり。
	それを前提としていることで、こんな感じで別途定義している。
 */
public class TypeIdentificationResolver {
	public static MessageType DetermineMessageType (byte[] data) {
		var dataStr = Encoding.UTF8.GetString(data);
		return JsonUtility.FromJson<MessageBase>(dataStr).type;
	}

	public static MessageType DetermineMessageType (MessageBase data) {
		return data.type;
	}

	public static MessageType DetermineMessageType<T> () where T : MessageBase, new() {
		return new T().type;
	}

	public static T Deserialize<T> (byte[] data) where T : MessageBase, new() {
		// bytes to str.
		var baseDataStr = Encoding.UTF8.GetString(data);

		// deserialize dataStr to specific typed instance.
		return JsonUtility.FromJson<T>(baseDataStr);
	}
}