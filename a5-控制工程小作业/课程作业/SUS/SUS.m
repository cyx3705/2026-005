s=tf('s');
%初始参数
P=400;%kg.良子与焖子
dmax=0.2;%最大刚性位移
m=300;%kg
c=80000;%N/m/s
k=100000;%N/m
SYS=(c*s+k)/(m*s^2+c*s+k);
%刚性条件约束
kmin=P/dmax;%最小悬挂刚性
%过减速带10km/h，
% 研究xt输入位移y是多少
function y = r(t, v)
    if t <= 0.1 / v
        y = v * t / 2;
    elseif t > 0.1 / v && t <= 0.2 / v
        y = 0.05;
    elseif t > 0.2 / v && t <= 0.3 / v
        y = 0.05 - (v * t - 0.2) / 2;
    else
        y = 0;
    end
end
%研究yt位移响应，调整k和c，实现最平稳ymax-ymin要小
%优化得到更好的kc和初始值进行量化比较

t=0:0.001:20;
xt=r(t,10)
for k=kmin:100:200000
    SYS=(c*s+k)/(m*s^2+c*s+k);
    yt=lsim(SYS,xt,t);
    %算ymax与y''max并记录max对应的k值
end
%在数组中比较选择合适的k
%同理，c系数也要改
%出图，量化;
yt=lsim(SYS,xt,t);
