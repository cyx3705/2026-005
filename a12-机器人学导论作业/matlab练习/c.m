% 分段三次多项式 c
clear; clc; close all;

%% 第一段 t ∈ [0,1]
t1 = linspace(0,1,50);
th1 = 60 + 202.5*t1.^2 - 142.5*t1.^3;
dth1 = 405*t1 - 427.5*t1.^2;
ddth1 = 405 - 855*t1;
dddth1 = -855*ones(size(t1));

%% 第二段 τ ∈ [0,1], t = 1+τ
tau = linspace(0,1,50);
t2 = 1 + tau;
th2 = 120 -22.5*tau -225*tau.^2 +157.5*tau.^3;
dth2 = -22.5 -450*tau +472.5*tau.^2;
ddth2 = -450 +945*tau;
dddth2 = 945*ones(size(tau));

% 拼接整条曲线
t_all = [t1, t2];
th_all = [th1, th2];
dth_all = [dth1, dth2];
ddth_all = [ddth1, ddth2];
dddth_all = [dddth1, dddth2];

% 绘图
figure('Color','w');
subplot(4,1,1);
plot(t_all,th_all,'LineWidth',1.5);
ylabel('\theta (^\circ)'); grid on; title('两段三次多项式轨迹');

subplot(4,1,2);
plot(t_all,dth_all,'r','LineWidth',1.5);
ylabel('\dot\theta (^\circ/s)'); grid on;

subplot(4,1,3);
plot(t_all,ddth_all,'b','LineWidth',1.5);
ylabel('\ddot\theta (^\circ/s^2)'); grid on;

subplot(4,1,4);
plot(t_all,dddth_all,'m','LineWidth',1.5);
xlabel('t (s)'); ylabel('\dddot\theta (^\circ/s^3)'); grid on;

% 打印衔接点数值，证明连续
fprintf('衔接点t=1:\n');
fprintf('第一段速度=%.2f, 第二段速度=%.2f\n',dth1(end),dth2(1));
fprintf('第一段加速度=%.2f, 第二段加速度=%.2f\n',ddth1(end),ddth2(1));