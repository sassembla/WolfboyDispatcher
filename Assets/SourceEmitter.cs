using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;


/*
	できてることがいくつかあって、
	・特定の型を基礎にしたmessageの区別
	・特定の型を基礎にしたmessageの処理

	・インスタンスが特定の型のmessageを受け取るReceiverを、特定の型から受け取るようにセット
	・特定の型から、受け取り者の型を指定してデータを流す
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
		// Dispatchers.DispatcherOf<WantToReceiveMessage1>().RemoveObserver<Message2>(u);


		// 擬似的なレシーブ動作
		var data = new Message1();
		data.param = "a";
		var jsonStr = JsonUtility.ToJson(data);
		var jsonBytes = Encoding.UTF8.GetBytes(jsonStr);

		var data2 = new Message2("fufufu");
		var jsonStr2 = JsonUtility.ToJson(data2);
		var jsonBytes2 = Encoding.UTF8.GetBytes(jsonStr2);

		for (var i = 0; i < 10; i++) {
			var dataBox = jsonBytes;
			if (i % 2 == 1) {
				dataBox = jsonBytes2;
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
				Dispatchers.DispatcherOf<SourceEmitter>().SendTo<WantToReceiveMessage1>(data);
				break;
			}
		}
	}
}


public class Dispatchers {
	/*
		データ分配を行う型に対して、ディスパッチャーを返す辞書
	 */
	private static Dictionary<Type, Dispatcher> typeToReceiverDict = new Dictionary<Type, Dispatcher>();

	/**
		特定の型のDispatcherを取得/生成する
	 */
	public static Dispatcher DispatcherOf<T> () {
		if (!typeToReceiverDict.ContainsKey(typeof(T))) {
			// create and set.
			typeToReceiverDict[typeof(T)] = new Dispatcher();
		}

		// return.
		return typeToReceiverDict[typeof(T)];
	}

	public class Dispatcher {
		
		private Dictionary<Type, Dictionary<MessageType, Action<byte[]>>> receiverType_messageType_deserializeActionDict = new Dictionary<Type, Dictionary<MessageType, Action<byte[]>>>();
		private Dictionary<Type, Dictionary<MessageType, Action<BaseMessage>>> receiverType_messageType_actionDict = new Dictionary<Type, Dictionary<MessageType, Action<BaseMessage>>>();

		/**
			セットされているレシーバーへと、byte[]を送付する
		 */
		public void SendTo<TypeOfReceiver> (byte[] data) {
			if (receiverType_messageType_deserializeActionDict.ContainsKey(typeof(TypeOfReceiver))) {

				// メッセージ型の特定
				var dataStr = Encoding.UTF8.GetString(data);
				var messageType = JsonUtility.FromJson<BaseMessage>(dataStr).type;

				var actionList = receiverType_messageType_deserializeActionDict[typeof(TypeOfReceiver)];
				if (actionList != null) {
					foreach (var action in actionList) {
						// このアクションのターゲットがこのデータの形式にマッチしなければ無視
						if (messageType != action.Key) continue;

						// 実行
						action.Value(data);
					}
				}
			}
		}

		/**
			デシリアライズが済んでいるデータをそのまま次のディスパッチャへと伝える
		 */
		public void Relay<TypeOfReceiver> (BaseMessage dataSource) {
			if (receiverType_messageType_actionDict.ContainsKey(typeof(TypeOfReceiver))) {
				var actionList = receiverType_messageType_actionDict[typeof(TypeOfReceiver)];
				foreach (var action in actionList) {
					// このアクションのターゲットがこのkindでなければ無視
					if (dataSource.type != action.Key) continue;

					// 実行
					action.Value(dataSource);
				}
			}
		}

		/**
			特定の型に対して、特定のメッセージ型が来た時に実行するメソッドを登録
		*/
		public void SetObserver<T> (Action<T> action) where T : BaseMessage, new() {
			Debug.Assert(action.Target != null, "action should be defined as function. not lambda.");
			var actionOwnerType = action.Target.GetType();
			var kind = new T().type;

			{
				if (!receiverType_messageType_actionDict.ContainsKey(actionOwnerType)) {
					receiverType_messageType_actionDict[actionOwnerType] = new Dictionary<MessageType, Action<BaseMessage>>();
				}

				/*
					non deserialized data transfer.
					deserialized data -> execute action.
				*/
				Action<BaseMessage> executeAct = deserializedData => {
					// execute act.
					action((T)deserializedData);
				};
				
				receiverType_messageType_actionDict[actionOwnerType][kind] = executeAct;
			}

			{
				if (!receiverType_messageType_deserializeActionDict.ContainsKey(actionOwnerType)) {
					receiverType_messageType_deserializeActionDict[actionOwnerType] = new Dictionary<MessageType, Action<byte[]>>();
				}

				/*
					deserialize and transfer action.
					byte -> data type -> execute action.
				*/
				Action<byte[]> deserializeAndExecuteAct = baseDataBytes => {


					// bytes to str.
					var baseDataStr = Encoding.UTF8.GetString(baseDataBytes);

					// deserialize dataStr to specific typed instance.
					var receivedData = JsonUtility.FromJson<T>(baseDataStr);

					// execute act.
					action((T)receivedData);
				};

				receiverType_messageType_deserializeActionDict[actionOwnerType][kind] = deserializeAndExecuteAct;
			}
		}

		public void RemoveObserver<T> (object actionOwner) where T : BaseMessage, new() {
			var actionOwnerType = actionOwner.GetType();
			var kind = new T().type;

			if (receiverType_messageType_actionDict.ContainsKey(actionOwnerType)) {
				receiverType_messageType_actionDict[actionOwnerType].Remove(kind);
			}

			if (receiverType_messageType_deserializeActionDict.ContainsKey(actionOwnerType)) {
				receiverType_messageType_deserializeActionDict[actionOwnerType].Remove(kind);
			}
		}
	}
}


/**
	この型は、特定のメッセージだけを、特定のオブザーバで受け取りたい。
 */
public class WantToReceiveMessage1 {
	public WantToReceiveMessage1 () {
		Dispatchers.DispatcherOf<SourceEmitter>().SetObserver<Message1>(ReceiveMessage1);
		Dispatchers.DispatcherOf<SourceEmitter>().SetObserver<Message2>(ReceiveMessage2);
	}

	public void ReceiveMessage1 (Message1 data) {
		Debug.LogError("ReceiveMessage1 received data:" + data.param);
	}

	public void ReceiveMessage2 (Message2 data) {
		Debug.LogError("ReceiveMessage2 received data:" + data.param2);

		// ここでは、すでにデータがデシリアライズされてしまっている。ので、これをそのまま次のディスパッチャに渡す方法が欲しい。
		Dispatchers.DispatcherOf<WantToReceiveMessage1>().Relay<WantToReceiveMessage2>(data);
	}
}

public class WantToReceiveMessage2 {
	public WantToReceiveMessage2 () {
		Dispatchers.DispatcherOf<WantToReceiveMessage1>().SetObserver<Message2>(Receiver);
	}

	public void Receiver (Message2 data) {
		Debug.LogError("WantToReceiveMessage2 received data:" + data.param2);
	}
}


/**
	enum定義
 */
public enum MessageType {
	Base,
	_1,
	_2
}


/**
	型定義
 */
public class BaseMessage {
	public MessageType type;
	public BaseMessage (MessageType type) {
		this.type = type;
	}
}

public class Message1 : BaseMessage {
	public string param;
	public Message1 () : base (MessageType._1) {

	}
}

public class Message2 : BaseMessage {
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


