using System;
using System.Collections.Generic;
using System.IO;
using AdoNetCore.AseClient.Enum;
using AdoNetCore.AseClient.Interface;
using AdoNetCore.AseClient.Token;

namespace AdoNetCore.AseClient.Internal
{
    internal class TokenReader : ITokenReader
    {
        private IFormatToken _previousFormatToken;

        public IEnumerable<IToken> Read(TokenReceiveStream stream, DbEnvironment env)
        {
            while (stream.DataAvailable)
            {
                var rawTokenType = (byte) stream.ReadByte();
                var tokenType = (TokenType)rawTokenType;

                if (Readers.ContainsKey(tokenType))
                {
                    var t = Readers[tokenType](stream, env, _previousFormatToken);

                    if (t is IFormatToken token)
                    {
                        Logger.Instance?.WriteLine($"**Set new format token**");
                        _previousFormatToken = token;
                    }

                    yield return t;
                }
                else
                {
                    Logger.Instance?.WriteLine($"!!! Hit unknown token type {tokenType} !!!");
                    var t = new CatchAllToken(rawTokenType);
                    t.Read(stream, env, _previousFormatToken);
                    yield return t;
                }

                if (stream.IsCancelled)
                {
                    Logger.Instance?.WriteLine($"{nameof(TokenReceiveStream)} - received cancel status flag");

                    yield return 
                        new DoneToken
                        {
                            Count = 0,
                            Status = DoneToken.DoneStatus.TDS_DONE_ATTN
                        };
                }
            }
            _previousFormatToken = null;
        }

        public IToken ReadOne(TokenReceiveStream stream, DbEnvironment env)
        {
            if (stream.DataAvailable)
            {
                var rawTokenType = (byte)stream.ReadByte();
                var tokenType = (TokenType)rawTokenType;
                IToken t;

                if (Readers.ContainsKey(tokenType))
                {
                    t = Readers[tokenType](stream, env, _previousFormatToken);

                    if (t is IFormatToken token)
                    {
                        Logger.Instance?.WriteLine($"**Set new format token**");
                        _previousFormatToken = token;
                    }
                }
                else
                {
                    Logger.Instance?.WriteLine($"!!! Hit unknown token type {tokenType} !!!");
                    t = new CatchAllToken(rawTokenType);
                    t.Read(stream, env, _previousFormatToken);
                }

                if (stream.IsCancelled)
                {
                    Logger.Instance?.WriteLine($"{nameof(TokenReceiveStream)} - received cancel status flag");

                    return
                        new DoneToken
                        {
                            Count = 0,
                            Status = DoneToken.DoneStatus.TDS_DONE_ATTN
                        };
                }
                return t;
            }
            _previousFormatToken = null;
            return null;
        }

        private static readonly Dictionary<TokenType, Func<Stream, DbEnvironment, IFormatToken, IToken>> Readers = new Dictionary<TokenType, Func<Stream, DbEnvironment, IFormatToken, IToken>>
        {
            {TokenType.TDS_ENVCHANGE, EnvironmentChangeToken.Create},
            {TokenType.TDS_EED, EedToken.Create },
            {TokenType.TDS_LOGINACK, LoginAckToken.Create },
            {TokenType.TDS_DONE, DoneToken.Create },
            {TokenType.TDS_CAPABILITY, ServerCapabilityToken.Create },
            {TokenType.TDS_RETURNSTATUS, ReturnStatusToken.Create },
            {TokenType.TDS_DONEINPROC, DoneInProcToken.Create },
            {TokenType.TDS_DONEPROC, DoneProcToken.Create },
            {TokenType.TDS_ROWFMT2, RowFormat2Token.Create },
            {TokenType.TDS_CONTROL, ControlToken.Create },
            {TokenType.TDS_ROW, RowToken.Create },
            {TokenType.TDS_PARAMFMT, ParameterFormatToken.Create },
            {TokenType.TDS_PARAMFMT2, ParameterFormat2Token.Create },
            {TokenType.TDS_PARAMS, ParametersToken.Create },
            {TokenType.TDS_OPTIONCMD, OptionCommandToken.Create },
            {TokenType.TDS_MSG, MessageToken.Create}
        };
    }
}
