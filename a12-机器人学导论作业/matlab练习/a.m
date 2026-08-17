% 单关节三次多项式轨迹 情况a
clear; clc; close all;

% 已知参数
theta_s = 120;  % 起始角度(deg)
theta_f = 60;   % 终止角度(deg)
tf = 1;         % 总时间(s)

% 多项式系数
a0 = theta_s;
a1 = 0;
a2 = 3*(theta_f-theta_s)/tf^2;  % 通用公式:3(θf-θs)/tf²
a3 = -2*(theta_f-theta_s)/tf^3; % 通用公式:-2(θf-θs)/tf³
fprintf('三次多项式系数：a0=%.2f, a1=%.2f, a2=%.2f, a3=%.2f\n',a0,a1,a2,a3);

% 时间采样
t = linspace(0, tf, 100);
% 各阶轨迹
theta = a0 + a1*t + a2*t.^2 + a3*t.^3;
dtheta = a1 + 2*a2*t + 3*a3*t.^2;
ddtheta = 2*a2 + 6*a3*t;
dddtheta = 6*a3*ones(size(t));

% 打印函数表达式
fprintf('关节角 θ(t) = %.0f + %.0f t + %.0f t² + %.0f t³ °\n',a0,a1,a2,a3);
fprintf('角速度 θ̇(t) = %.0f + %.0f t + %.0f t² °/s\n',a1,2*a2,3*a3);
fprintf('角加速度 θ̈(t) = %.0f + %.0f t °/s²\n',2*a2,6*a3);
fprintf('加加速度 θ̈̇(t) = %.0f °/s³\n',6*a3);

% 分4张子图竖直绘图subplot(4,1,i)
figure('Color','w');
subplot(4,1,1);
plot(t,theta,'LineWidth',1.5);
ylabel('关节角 \theta (^\circ)');
title('三次多项式轨迹：角度、角速度、角加速度、加加速度');
grid on;

subplot(4,1,2);
plot(t,dtheta,'LineWidth',1.5,'Color','#d95319');
ylabel('角速度 \dot{\theta} (^\circ/s)');
grid on;

subplot(4,1,3);
plot(t,ddtheta,'LineWidth',1.5,'Color','#0072bd');
ylabel('角加速度 \ddot{\theta} (^\circ/s^2)');
grid on;

subplot(4,1,4);
plot(t,dddtheta,'LineWidth',1.5,'Color','#edb120');
xlabel('时间 t (s)');
ylabel('加加速度 \dddot{\theta} (^\circ/s^3)');
grid on;
