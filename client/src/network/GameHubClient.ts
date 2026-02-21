import { HubConnectionBuilder, HubConnection } from '@microsoft/signalr';
import { MessagePackHubProtocol } from '@microsoft/signalr-protocol-msgpack';
import { getToken } from './RestClient';

/**
 * SignalR + MessagePack hub client.
 * JWT passed as query param: ?access_token=... on WebSocket upgrade.
 * Server→Client messages: PascalCase  (e.g. WorldStateUpdate)
 * Client→Server methods: PascalCase  (e.g. SendInput)
 */
export class GameHubClient {
  private connection: HubConnection;

  constructor() {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/game', {
        accessTokenFactory: () => getToken() ?? '',
      })
      .withHubProtocol(new MessagePackHubProtocol())
      .withAutomaticReconnect()
      .build();
  }

  getConnection(): HubConnection {
    return this.connection;
  }
}

