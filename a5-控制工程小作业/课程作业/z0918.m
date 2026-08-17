% 增加参数取值密度，形成更密集的网格
m_values = 1:2:9;    % m = 1,3,5,7,9
c_values = 1:2:9;    % c = 1,3,5,7,9
k_values = 1:2:9;    % k = 1,3,5,7,9

% 时间向量和输入信号
t = 0:0.01:15;       % 时间范围
u = ones(size(t));   % 阶跃输入

% 创建图形窗口并设置大小
figure('Position', [100, 100, 1000, 700]);
hold on;
grid on;
box on;

% 定义视觉编码方案，增强参数区分度
% 用颜色区分m值（使用更明显的颜色梯度）
m_colors = lines(length(m_values));
% 用线型区分c值
line_styles = {'-', '--', ':', '-.'};
% 用标记区分k值
markers = {'o', 's', '^', 'd', 'p'};

% 三重循环遍历所有参数组合
for i = 1:length(m_values)
    m = m_values(i);
    current_color = m_colors(i,:);  % 为每个m分配独特颜色
    
    for j = 1:length(c_values)
        c = c_values(j);
        % 循环使用线型（超过数量时循环）
        ls_idx = mod(j-1, length(line_styles)) + 1;
        current_ls = line_styles{ls_idx};
        
        for k_idx = 1:length(k_values)
            k = k_values(k_idx);
            % 循环使用标记（超过数量时循环）
            mk_idx = mod(k_idx-1, length(markers)) + 1;
            current_marker = markers{mk_idx};
            
            % 计算系统传递函数
            s = tf('s');
            G = 1 / (m*s^2 + c*s + k);
            
            % 求解系统响应
            [y, t] = lsim(G, u, t);
            
            % 绘制曲线，每100个点显示一个标记以避免过于密集
            plot(t, y, ...
                 'Color', current_color, ...        % 颜色区分m
                 'LineStyle', current_ls, ...       % 线型区分c
                 'Marker', current_marker, ...      % 标记区分k
                 'MarkerSize', 3, ...
                 'MarkerIndices', 1:100:length(t), ...  % 间隔显示标记
                 'LineWidth', 0.8, ...
                 'DisplayName', sprintf('m=%d, c=%d, k=%d', m, c, k));
        end
    end
end

% 添加图例和标签（分组显示以提高可读性）
legend('Location', 'bestoutside', 'FontSize', 8);
xlabel('时间 (s)', 'FontSize', 12);
ylabel('系统响应', 'FontSize', 12);
title('不同 m、c、k 组合的系统响应网图', 'FontSize', 14);

% 添加参数说明文本框，解释视觉编码
annotation('textbox', [0.02, 0.02, 0.2, 0.15], ...
    'String', {
        '参数视觉编码:'
        '• 颜色: 不同m值'
        '• 线型: 不同c值'
        '• 标记: 不同k值'
    }, ...
    'FontSize', 10, ...
    'EdgeColor', 'black', ...
    'BackgroundColor', 'white');

hold off;