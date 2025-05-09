import pandas as pd
import matplotlib.pyplot as plt
import os

plt.style.use('seaborn-v0_8-whitegrid')
plt.rcParams.update({
    'font.size': 12,
    'lines.markersize': 8,
    'lines.linewidth': 1.5,
    'figure.figsize': (10, 6),
    'axes.titlesize': 14,
    'axes.labelsize': 12
})

df = pd.read_csv(
    'data.txt',
    sep=r'\s+',
    header=None,
    decimal=',',
    names=[
        'lambda', 'mu', 'P0_theory', 'Pn_theory', 'Q_theory',
        'A_theory', 'k_theory', 'P0_est', 'Pn_est',
        'Q_est', 'A_est', 'k_est'
    ]
)
df = df.sort_values('lambda')


metrics_info = [
    ('P0', 'Вероятность простоя', 'вероятности простоя'),
    ('Pn', 'Вероятность отказа', 'вероятности отказа'),
    ('Q', 'Относительная пропускная способность', 'относительной пропускной способности'),
    ('A', 'Абсолютная пропускная способность', 'абсолютной пропускной способности'),
    ('k', 'Среднее число занятых каналов', 'среднего числа занятых каналов')
]


for i, (metric, ylabel, title_part) in enumerate(metrics_info, start=1):
    fig, ax = plt.subplots()
    ax.plot(
        df['lambda'],
        df[f'{metric}_theory'],
        color='gray',
        linestyle='--',
        label='Теоретические значения'
    )
    ax.plot(
        df['lambda'],
        df[f'{metric}_est'],
        color='purple',
        linestyle='-',
        marker='o',
        markersize=5, 
        label='Экспериментальные значения'
    )
    
    ax.set(
        xlabel='Интенсивность входного потока (λ)',
        ylabel=ylabel,
        title=f'Зависимость {title_part} от интенсивности потока'
    )
    
    ax.legend()
    ax.grid(True, alpha=0.3)
    output_path = os.path.join('result', f'p-{i}.png')
    fig.savefig(output_path, dpi=300, bbox_inches='tight')
    plt.close(fig)

print(f"Графики успешно построены и сохранены")