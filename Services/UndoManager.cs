using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MapaMaquinas.Controls;

namespace MapaMaquinas.Services
{
    /// <summary>
    /// Pilha de desfazer para movimentação de cards no mapa.
    /// Registra a posição anterior antes de cada drag e restaura com Ctrl+Z.
    /// </summary>
    public class UndoManager
    {
        private record EntradaUndo(CardMaquina Card, double X, double Y);

        private readonly Stack<EntradaUndo> _pilha = new();
        private const int MaxEntradas = 50;

        public bool PodeDesfazer => _pilha.Count > 0;
        public int  Quantidade    => _pilha.Count;

        /// <summary>
        /// Registra a posição atual do card ANTES de mover.
        /// Chamar no início do drag (MouseLeftButtonDown).
        /// </summary>
        public void Registrar(CardMaquina card)
        {
            if (_pilha.Count >= MaxEntradas)
            {
                // Remove o mais antigo — Stack não tem RemoveLast,
                // então recria invertida sem o fundo
                var temp = new List<EntradaUndo>(_pilha);
                temp.RemoveAt(temp.Count - 1);
                _pilha.Clear();
                // Reempilha na ordem correta (mais recente no topo)
                for (int i = temp.Count - 1; i >= 0; i--)
                    _pilha.Push(temp[i]);
            }

            _pilha.Push(new EntradaUndo(
                card,
                Canvas.GetLeft(card),
                Canvas.GetTop(card)));
        }

        /// <summary>
        /// Desfaz o último movimento. Restaura posição no Canvas e no modelo.
        /// Retorna o card afetado ou null se a pilha estiver vazia.
        /// </summary>
        public CardMaquina? Desfazer()
        {
            if (!PodeDesfazer) return null;

            var entrada = _pilha.Pop();
            Canvas.SetLeft(entrada.Card, entrada.X);
            Canvas.SetTop(entrada.Card,  entrada.Y);
            entrada.Card.SalvarPosicao();   // atualiza modelo (PosX/PosY)
            return entrada.Card;
        }

        /// <summary>Limpa a pilha ao trocar de empresa ou recarregar o mapa.</summary>
        public void Limpar() => _pilha.Clear();
    }
}
